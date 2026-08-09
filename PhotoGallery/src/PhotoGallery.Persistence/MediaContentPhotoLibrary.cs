using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using Tizen.Content.MediaContent;

namespace PhotoGallery.Persistence;

/// <summary>
/// Reads the device media database through TizenFX. This adapter deliberately
/// projects only non-sensitive gallery fields; location metadata is not exposed.
/// </summary>
public sealed class MediaContentPhotoLibrary : IPhotoLibrary
{
    private const int MaximumSnapshotSize = 5_000;

    public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => ReadSnapshot(cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<PhotoRecord> ReadSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var database = new MediaDatabase();
        database.Connect();
        var command = new MediaInfoCommand(database);
        using var reader = command.SelectMedia();
        var photos = new List<PhotoRecord>();

        while (photos.Count < MaximumSnapshotSize && reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var media = reader.Current;
            if (media.MediaType != MediaType.Image ||
                string.IsNullOrWhiteSpace(media.Id) ||
                media.Id.Length > PhotoRecord.MaximumIdLength)
            {
                continue;
            }

            photos.Add(PhotoRecord.Create(
                media.Id,
                string.IsNullOrWhiteSpace(media.Title) ? media.DisplayName : media.Title,
                media.DateModified,
                location: string.Empty,
                path: media.Path,
                note: string.Empty));
        }

        return photos;
    }
}
