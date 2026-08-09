namespace PhotoGallery.Domain;

public sealed record PhotoResolution(IReadOnlyList<PhotoRecord> Photos, IReadOnlyList<string> UnresolvedIds);

public static class PhotoResolver
{
    public const int MaximumIdsPerRequest = 100;

    public static PhotoResolution ResolveByIds(IEnumerable<PhotoRecord> photos, IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count > MaximumIdsPerRequest || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > PhotoRecord.MaximumIdLength))
        {
            throw new ArgumentException($"Provide at most {MaximumIdsPerRequest} non-empty photo IDs, each at most {PhotoRecord.MaximumIdLength} characters.", nameof(ids));
        }

        var byId = photos.ToDictionary(photo => photo.Id, StringComparer.Ordinal);
        var resolved = new List<PhotoRecord>();
        var unresolved = new List<string>();
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var photo))
            {
                resolved.Add(photo);
            }
            else
            {
                unresolved.Add(id);
            }
        }

        return new PhotoResolution(resolved, unresolved);
    }
}
