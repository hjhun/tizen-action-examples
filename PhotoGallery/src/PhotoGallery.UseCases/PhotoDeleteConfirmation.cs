using PhotoGallery.Domain;

namespace PhotoGallery.UseCases;

public sealed class PhotoDeleteConfirmation
{
    public string TargetId { get; }

    public PhotoDeleteConfirmation(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || targetId.Length > PhotoRecord.MaximumIdLength)
            throw new ArgumentException("Provide a valid photo ID.", nameof(targetId));
        TargetId = targetId;
    }

    public PhotoRecord Resolve(IReadOnlyList<PhotoRecord> snapshot)
    {
        var photo = snapshot.FirstOrDefault(x => x.Id == TargetId)
            ?? throw new InvalidOperationException("The photo selected for deletion is no longer available. Cancel this confirmation.");
        if (!photo.Owned)
            throw new InvalidOperationException("The photo selected for deletion is no longer an imported copy. Cancel this confirmation.");
        return photo;
    }
}
