namespace PhotoGallery.Domain;

public sealed record PhotoRecord(
    string Id,
    string Title,
    DateTimeOffset CapturedAt,
    string Location,
    string Path,
    string Note)
{
    public bool Favorite { get; init; }
    public bool Owned { get; init; }
    public string Album { get; init; } = string.Empty;
    public string MimeType { get; init; } = "image/png";
    public long FileSize { get; init; }
    public string StorageType { get; init; } = "internal";

    public const int MaximumIdLength = 256;

    public static PhotoRecord Create(
        string id,
        string? title,
        DateTimeOffset capturedAt,
        string? location,
        string? path,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaximumIdLength)
        {
            throw new ArgumentException($"A stable photo ID of at most {MaximumIdLength} characters is required.", nameof(id));
        }

        return new PhotoRecord(
            id,
            title?.Trim() ?? string.Empty,
            capturedAt,
            location?.Trim() ?? string.Empty,
            path?.Trim() ?? string.Empty,
            note?.Trim() ?? string.Empty);
    }
}
