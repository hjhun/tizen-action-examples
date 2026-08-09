using PhotoGallery.Domain;

namespace PhotoGallery.UseCases;

public sealed record PhotoQueryResult(IReadOnlyList<PhotoRecord> Photos, bool IsCurrent);

public sealed class PhotoQueryService
{
    private readonly IPhotoLibrary _library;
    private long _latestRequest;

    public PhotoQueryService(IPhotoLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public async Task<PhotoQueryResult> SearchAsync(PhotoSearchCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var request = Interlocked.Increment(ref _latestRequest);
        var snapshot = await _library.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = snapshot
            .Where(photo => Matches(photo, criteria))
            .OrderByDescending(photo => photo.CapturedAt)
            .ThenBy(photo => photo.Id, StringComparer.Ordinal)
            .Take(criteria.Limit)
            .ToArray();
        return new PhotoQueryResult(matches, request == Volatile.Read(ref _latestRequest));
    }

    private static bool Matches(PhotoRecord photo, PhotoSearchCriteria criteria)
    {
        if (criteria.FromInclusive is not null && photo.CapturedAt < criteria.FromInclusive ||
            criteria.UntilExclusive is not null && photo.CapturedAt >= criteria.UntilExclusive)
        {
            return false;
        }

        return criteria.Keyword.Length == 0 ||
            photo.Title.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase) ||
            photo.Location.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase) ||
            photo.Note.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase);
    }
}
