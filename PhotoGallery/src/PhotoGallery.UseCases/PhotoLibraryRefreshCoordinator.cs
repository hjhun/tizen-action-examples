using PhotoGallery.Domain;

namespace PhotoGallery.UseCases;

/// <summary>
/// Owns one lifecycle-bound media refresh at a time. A newer refresh, explicit
/// invalidation, or disposal cancels work that can no longer update a visible surface.
/// </summary>
public sealed class PhotoLibraryRefreshCoordinator : IDisposable
{
    private readonly IPhotoLibrary _library;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _activeRefresh;
    private long _generation;
    private bool _disposed;

    public PhotoLibraryRefreshCoordinator(IPhotoLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _generation++;
            _activeRefresh?.Cancel();
        }
    }

    public async Task<PhotoLibraryRefreshResult> RefreshAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource refresh;
        long generation;

        lock (_gate)
        {
            ThrowIfDisposed();
            _activeRefresh?.Cancel();
            refresh = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            _activeRefresh = refresh;
            generation = ++_generation;
        }

        try
        {
            var photos = await _library.ReadSnapshotAsync(refresh.Token).ConfigureAwait(false);
            refresh.Token.ThrowIfCancellationRequested();
            return new PhotoLibraryRefreshResult(photos, IsCurrent(generation, refresh));
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeRefresh, refresh))
                {
                    _activeRefresh = null;
                }
            }

            refresh.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            _activeRefresh?.Cancel();
            _lifetime.Cancel();
        }

        _lifetime.Dispose();
    }

    private bool IsCurrent(long generation, CancellationTokenSource refresh)
    {
        lock (_gate)
        {
            return !_disposed && generation == _generation && ReferenceEquals(_activeRefresh, refresh);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed record PhotoLibraryRefreshResult(IReadOnlyList<PhotoRecord> Photos, bool IsCurrent);
