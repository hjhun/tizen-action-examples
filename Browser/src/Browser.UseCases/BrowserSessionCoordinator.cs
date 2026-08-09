using Browser.Persistence;

namespace Browser.UseCases;

public interface IBrowserSessionStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(string serializedSnapshot, CancellationToken cancellationToken);
}

public enum BrowserSessionRestoreStatus
{
    Restored,
    NoSession,
    Superseded
}

public sealed record BrowserSessionRestoreResult(BrowserSessionRestoreStatus Status, BrowserSessionSnapshot? Snapshot);

public enum BrowserSessionSaveStatus
{
    Saved,
    Superseded
}

public sealed record BrowserSessionSaveResult(BrowserSessionSaveStatus Status);

public sealed class BrowserSessionCoordinator : IAsyncDisposable
{
    private readonly IBrowserSessionStore _store;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private long _latestOperationId;
    private int _disposed;

    public BrowserSessionCoordinator(IBrowserSessionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<BrowserSessionRestoreResult> RestoreAsync(CancellationToken cancellationToken)
    {
        var operationId = BeginOperation();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var serialized = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSuperseded(operationId))
            {
                return new BrowserSessionRestoreResult(BrowserSessionRestoreStatus.Superseded, null);
            }

            return string.IsNullOrWhiteSpace(serialized)
                ? new BrowserSessionRestoreResult(BrowserSessionRestoreStatus.NoSession, null)
                : new BrowserSessionRestoreResult(
                    BrowserSessionRestoreStatus.Restored,
                    BrowserSessionSnapshotSerializer.Deserialize(serialized));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<BrowserSessionSaveResult> SaveAsync(
        BrowserSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var operationId = BeginOperation();
        var serialized = BrowserSessionSnapshotSerializer.Serialize(snapshot);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync(serialized, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return new BrowserSessionSaveResult(
                IsSuperseded(operationId) ? BrowserSessionSaveStatus.Superseded : BrowserSessionSaveStatus.Saved);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        _operationGate.Release();
        _operationGate.Dispose();
    }

    private long BeginOperation()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Interlocked.Increment(ref _latestOperationId);
    }

    private bool IsSuperseded(long operationId) => Volatile.Read(ref _latestOperationId) != operationId;
}
