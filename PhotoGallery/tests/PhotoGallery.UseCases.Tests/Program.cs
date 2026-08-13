using System.Diagnostics;
using PhotoGallery.Domain;
using PhotoGallery.UseCases;

var diagnostic = PhotoActionDiagnostics.Format(
    "Tv_Tizen.Action.Photo_Search",
    PhotoActionDiagnostics.Correlate("media-safe-id"),
    "valid",
    "completed",
    "success",
    Stopwatch.StartNew(),
    new InvalidOperationException("/media/private/photo?query=secret"));
if (!diagnostic.Contains("action=Tv_Tizen.Action.Photo_Search", StringComparison.Ordinal) ||
    diagnostic.Contains("media-safe-id", StringComparison.Ordinal) ||
    diagnostic.Contains("/media/", StringComparison.Ordinal) ||
    diagnostic.Contains("query=secret", StringComparison.Ordinal) ||
    !diagnostic.Contains("exception=InvalidOperationException", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Action diagnostics must retain required fields without raw media or query content.");
}

var batchCorrelation = PhotoActionDiagnostics.Correlate(new[] { "media-a", "media-b" });
if (!batchCorrelation.StartsWith("set-2-", StringComparison.Ordinal) ||
    batchCorrelation.Contains("media-a", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Batch Action diagnostics must only retain a bounded hashed correlation ID.");
}

if (PhotoActionDiagnostics.Correlate((string?)null) != "none" ||
    PhotoActionDiagnostics.Correlate(string.Empty) != "none" ||
    PhotoActionDiagnostics.Correlate(Array.Empty<string>()) != "none")
{
    throw new InvalidOperationException("Empty Action identifiers must have an explicit safe correlation value.");
}

var firstRead = new TaskCompletionSource<IReadOnlyList<PhotoRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);
var library = new SequencedLibrary(
    firstRead.Task,
    [PhotoRecord.Create("newer", "Sunset", new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero), "Jeju", "/media/newer", string.Empty)]);
var service = new PhotoQueryService(library);
var criteria = PhotoSearchCriteria.Create(null, null, null, 20);
var staleTask = service.SearchAsync(criteria, CancellationToken.None);
await library.WaitForFirstReadAsync();
var current = await service.SearchAsync(criteria, CancellationToken.None);
firstRead.SetResult([PhotoRecord.Create("older", "Garden", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), "Seoul", "/media/older", string.Empty)]);
var stale = await staleTask;

if (!current.IsCurrent || current.Photos.Single().Id != "newer" || stale.IsCurrent)
{
    throw new InvalidOperationException("A superseded media query must not publish a stale completion.");
}

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try
{
    await service.SearchAsync(criteria, cancelled.Token);
    throw new InvalidOperationException("A cancelled query must propagate cancellation.");
}
catch (OperationCanceledException)
{
}

var coordinatorLibrary = new BlockingThenResultLibrary(
    [PhotoRecord.Create("fresh", "Ocean", new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero), string.Empty, "/media/fresh", string.Empty)]);
using (var coordinator = new PhotoLibraryRefreshCoordinator(coordinatorLibrary))
{
    var supersededRefresh = coordinator.RefreshAsync(CancellationToken.None);
    await coordinatorLibrary.WaitForFirstReadAsync();
    var currentRefresh = coordinator.RefreshAsync(CancellationToken.None);

    await AssertCancelledAsync(supersededRefresh, "A new refresh must cancel its superseded media read.");
    var refreshed = await currentRefresh;
    if (!refreshed.IsCurrent || refreshed.Photos.Single().Id != "fresh")
    {
        throw new InvalidOperationException("The newest refresh must be the only current snapshot.");
    }
}

var invalidatedLibrary = new BlockingThenResultLibrary(Array.Empty<PhotoRecord>());
using (var coordinator = new PhotoLibraryRefreshCoordinator(invalidatedLibrary))
{
    var invalidatedRefresh = coordinator.RefreshAsync(CancellationToken.None);
    await invalidatedLibrary.WaitForFirstReadAsync();
    coordinator.Invalidate();
    await AssertCancelledAsync(invalidatedRefresh, "Invalidation must cancel an obsolete media read.");
}

var disposedLibrary = new BlockingThenResultLibrary(Array.Empty<PhotoRecord>());
var disposedCoordinator = new PhotoLibraryRefreshCoordinator(disposedLibrary);
var disposedRefresh = disposedCoordinator.RefreshAsync(CancellationToken.None);
await disposedLibrary.WaitForFirstReadAsync();
disposedCoordinator.Dispose();
await AssertCancelledAsync(disposedRefresh, "Disposal must cancel an active media read.");
try
{
    await disposedCoordinator.RefreshAsync(CancellationToken.None);
    throw new InvalidOperationException("A disposed coordinator must reject new refreshes.");
}
catch (ObjectDisposedException)
{
}

Console.WriteLine("PhotoGallery.UseCases.Tests PASS");

static async Task AssertCancelledAsync(Task task, string failureMessage)
{
    try
    {
        await task;
        throw new InvalidOperationException(failureMessage);
    }
    catch (OperationCanceledException)
    {
    }
}

sealed class SequencedLibrary : IPhotoLibrary
{
    private readonly Task<IReadOnlyList<PhotoRecord>> _first;
    private readonly IReadOnlyList<PhotoRecord> _second;
    private readonly TaskCompletionSource _firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _reads;

    public SequencedLibrary(Task<IReadOnlyList<PhotoRecord>> first, IReadOnlyList<PhotoRecord> second)
    {
        _first = first;
        _second = second;
    }

    public Task WaitForFirstReadAsync() => _firstStarted.Task;

    public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Interlocked.Increment(ref _reads) == 1
            ? StartFirstAsync(cancellationToken)
            : Task.FromResult(_second);
    }

    private async Task<IReadOnlyList<PhotoRecord>> StartFirstAsync(CancellationToken cancellationToken)
    {
        _firstStarted.SetResult();
        return await _first.WaitAsync(cancellationToken);
    }
}

sealed class BlockingThenResultLibrary : IPhotoLibrary
{
    private readonly IReadOnlyList<PhotoRecord> _result;
    private readonly TaskCompletionSource _firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _reads;

    public BlockingThenResultLibrary(IReadOnlyList<PhotoRecord> result)
    {
        _result = result;
    }

    public Task WaitForFirstReadAsync() => _firstStarted.Task;

    public async Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _reads) == 1)
        {
            _firstStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _result;
    }
}
