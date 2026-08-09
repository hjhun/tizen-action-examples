namespace PhotoGallery.Domain;

public sealed record PhotoRecord(
    string Id,
    string Title,
    DateTimeOffset CapturedAt,
    string Location,
    string Path,
    string Note)
{
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
