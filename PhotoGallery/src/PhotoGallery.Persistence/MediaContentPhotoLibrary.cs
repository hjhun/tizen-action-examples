using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using Tizen.Content.MediaContent;

namespace PhotoGallery.Persistence;

/// <summary>Real MediaContent IDs and image files; only imported copies can be deleted.</summary>
public sealed class MediaContentPhotoLibrary : IMutablePhotoLibrary
{
    private readonly Lazy<PhotoMetadataStore> _metadata;
    private PhotoMetadataStore Metadata => _metadata.Value;
    private readonly string _ownedRoot;
    private readonly (string Root, string Type)[] _sourceRoots;
    public MediaContentPhotoLibrary(string dataDirectory, string picturesDirectory, IEnumerable<(string Root, string Type)> sourceRoots)
    {
        _metadata = new Lazy<PhotoMetadataStore>(() => new PhotoMetadataStore(Path.Combine(dataDirectory, "gallery-metadata.json")));
        _ownedRoot = Path.Combine(picturesDirectory, "PhotoGallery");
        _sourceRoots = sourceRoots.Select(x => (Path.GetFullPath(x.Root), x.Type)).ToArray();
    }
    public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken token) => Task.Run<IReadOnlyList<PhotoRecord>>(() =>
    {
        var metadataStore = Metadata; // Load bounded metadata on the worker so errors reach the UI recovery state.
        using var database = Open();
        using var reader = new MediaInfoCommand(database).SelectMedia();
        var photos = new List<PhotoRecord>(); var examined = 0;
        while (examined++ < 20000 && photos.Count < 5000 && reader.Read())
        {
            token.ThrowIfCancellationRequested();
            var media = reader.Current;
            if (media.MediaType != MediaType.Image || string.IsNullOrWhiteSpace(media.Id)
                || media.Id.Length > PhotoRecord.MaximumIdLength || media.FileSize > int.MaxValue || !File.Exists(media.Path)) continue;
            var storage = _sourceRoots.FirstOrDefault(x => IsInside(media.Path, x.Root));
            if (storage.Root is null) continue; // Only File.StorageType-representable storage is advertised.
            var metadata = metadataStore.Get(media.Id);
            var owned = metadata?.OwnedPath == media.Path && IsInside(media.Path, _ownedRoot);
            photos.Add(PhotoRecord.Create(media.Id, metadata?.Title ?? media.Title ?? media.DisplayName,
                media.DateModified, "", media.Path, "") with
            {
                Favorite = metadata?.Favorite ?? false, Owned = owned,
                Album = owned ? "Gallery" : Path.GetFileName(Path.GetDirectoryName(media.Path)) ?? "Pictures",
                MimeType = media.MimeType ?? "image/png", FileSize = media.FileSize, StorageType = storage.Type,
            });
        }
        return photos;
    }, token);

    public Task ImportAsync(string path, string title, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(path) || !_sourceRoots.Any(root => IsInside(path, root.Root)))
            throw new ArgumentException("Choose an image in device media storage.");
        var source = Path.GetFullPath(path);
        // Reject symbolic path components so the storage boundary cannot be bypassed.
        for (var part = new FileInfo(source) as FileSystemInfo; part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if (part.LinkTarget is not null) throw new ArgumentException("Symbolic image paths are not supported.");
        var info = new FileInfo(source);
        if (!info.Exists || info.Length is <= 0 or > 33554432) throw new ArgumentException("Choose an existing image of at most 32 MiB.");
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg")) throw new ArgumentException("Choose a JPEG or PNG image.");
        using (var input = File.OpenRead(source))
        {
            Span<byte> header = stackalloc byte[8];
            if (input.Read(header) != 8 || !(header.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
                || header[0] == 255 && header[1] == 216 && header[2] == 255))
                throw new ArgumentException("The file is not a JPEG or PNG image.");
        }
        Directory.CreateDirectory(_ownedRoot);
        var destination = Path.Combine(_ownedRoot, Guid.NewGuid().ToString("N") + extension);
        string? addedId = null;
        using var database = Open(); var command = new MediaInfoCommand(database);
        try
        {
            File.Copy(source, destination, false);
            token.ThrowIfCancellationRequested();
            var media = command.Add(destination);
            addedId = media.Id;
            if (media.MediaType != MediaType.Image) throw new ArgumentException("MediaContent could not decode this image.");
            Metadata.Set(media.Id, new PhotoMetadata(destination,
                string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(source) : title.Trim(), false));
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            if (addedId is not null) command.Delete(addedId);
            throw;
        }
    }, token);

    public Task DeleteAsync(string id, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var metadata = Metadata.Get(id);
        if (metadata is null || !IsInside(metadata.OwnedPath, _ownedRoot))
            throw new InvalidOperationException("Only imported copies can be deleted.");
        using var database = Open(); var command = new MediaInfoCommand(database);
        var media = command.SelectMedia(id);
        if (media is null || media.Path != metadata.OwnedPath) throw new InvalidOperationException("The photo is no longer available.");
        if (new FileInfo(media.Path).LinkTarget is not null) throw new InvalidOperationException("The image path changed.");
        // Retain a hidden app-owned copy until MediaContent acknowledges removal, allowing compensation.
        var backup = Path.Combine(Path.GetDirectoryName(_ownedRoot)!, ".gallery-delete-" + Guid.NewGuid().ToString("N"));
        File.Move(media.Path, backup);
        try
        {
            if (!command.Delete(id)) throw new InvalidOperationException("The photo was not found in MediaContent.");
        }
        catch
        {
            File.Move(backup, media.Path);
            command.Add(media.Path);
            throw;
        }
        File.Delete(backup);
        Metadata.Remove(id);
    }, token);

    public Task SetFavoriteAsync(string id, bool favorite, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        using var database = Open();
        var media = new MediaInfoCommand(database).SelectMedia(id)
            ?? throw new InvalidOperationException("The photo is no longer available.");
        var old = Metadata.Get(id) ?? new PhotoMetadata("", media.Title ?? media.DisplayName, false);
        Metadata.Set(id, old with { Favorite = favorite });
    }, token);

    private static MediaDatabase Open() { var database = new MediaDatabase(); try { database.Connect(); return database; } catch { database.Dispose(); throw; } }
    private static bool IsInside(string path, string root) => !string.IsNullOrWhiteSpace(path) &&
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
