using PhotoGallery.Domain;
using PhotoGallery.UseCases;

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

Console.WriteLine("PhotoGallery.UseCases.Tests PASS");

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
