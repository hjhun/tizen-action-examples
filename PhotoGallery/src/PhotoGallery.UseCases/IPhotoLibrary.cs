using PhotoGallery.Domain;

namespace PhotoGallery.UseCases;

/// <summary>Real MediaContent adapters implement this boundary; the domain never owns media metadata.</summary>
public interface IPhotoLibrary
{
    Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken cancellationToken);
}
