using System.Text.Json;

namespace PhotoGallery.Persistence;

public sealed record PhotoMetadata(string OwnedPath, string Title, bool Favorite);

public sealed class PhotoMetadataStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, PhotoMetadata> _items;
    public PhotoMetadataStore(string path)
    {
        _path = path;
        if (File.Exists(path) && new FileInfo(path).Length > 4 * 1024 * 1024)
            throw new IOException("Gallery metadata exceeds its size limit.");
        _items = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, PhotoMetadata>>(File.ReadAllText(path))
            ?? throw new JsonException("Invalid gallery metadata.") : new(StringComparer.Ordinal);
    }
    public PhotoMetadata? Get(string id) { lock (_gate) return _items.GetValueOrDefault(id); }
    public void Set(string id, PhotoMetadata metadata)
    {
        lock (_gate)
        {
            if (_items.Count >= 5000 && !_items.ContainsKey(id)) throw new IOException("Gallery metadata is full.");
            var next = new Dictionary<string, PhotoMetadata>(_items, StringComparer.Ordinal) { [id] = metadata };
            Persist(next);
        }
    }
    public void Remove(string id)
    {
        lock (_gate) { var next = new Dictionary<string, PhotoMetadata>(_items, StringComparer.Ordinal); next.Remove(id); Persist(next); }
    }
    private void Persist(Dictionary<string, PhotoMetadata> next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".pending";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, next); file.Flush(true); }
            File.Move(temporary, _path, true);
            _items = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
