using PhotoGallery.Domain;

namespace PhotoGallery.UseCases;

public interface IMutablePhotoLibrary : IPhotoLibrary
{
    Task ImportAsync(string path, string title, CancellationToken token);
    Task DeleteAsync(string id, CancellationToken token);
    Task SetFavoriteAsync(string id, bool favorite, CancellationToken token);
}

/// <summary>One shared, synchronized library and viewer state for UI and providers.</summary>
public sealed class GalleryLibraryService
{
    private readonly IMutablePhotoLibrary _library;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly object _gate = new();
    private PhotoRecord[] _photos = [];
    private string? _currentId;
    private bool _slideshow;
    private bool _ready;
    public event Action? Changed;
    public GalleryLibraryService(IMutablePhotoLibrary library) => _library = library;
    public bool Ready { get { lock (_gate) return _ready; } }
    public bool Slideshow { get { lock (_gate) return _slideshow; } }
    public IReadOnlyList<PhotoRecord> Snapshot { get { lock (_gate) return _photos.ToArray(); } }

    public Task RefreshAsync(CancellationToken token) => ExecuteAsync(null, token);
    public Task ImportAsync(string path, string title, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 2048 || title.Length > 200)
            throw new ArgumentException("Provide a local image path and a title of at most 200 characters.");
        return ExecuteAsync(ct => _library.ImportAsync(path, title, ct), token);
    }
    public Task DeleteAsync(string id, CancellationToken token)
    {
        var photo = Find(id);
        if (!photo.Owned) throw new InvalidOperationException("Only photos imported into this gallery can be deleted.");
        return ExecuteAsync(ct => _library.DeleteAsync(id, ct), token);
    }
    public Task SetFavoriteAsync(string id, bool value, CancellationToken token)
    {
        Find(id);
        return ExecuteAsync(ct => _library.SetFavoriteAsync(id, value, ct), token);
    }
    private async Task ExecuteAsync(Func<CancellationToken, Task>? mutation, CancellationToken token)
    {
        await _operations.WaitAsync(token).ConfigureAwait(false);
        var published = false;
        async Task Reload(CancellationToken ct)
        {
            var next = await _library.ReadSnapshotAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _photos = next.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
                _ready = true;
                if (_currentId is not null && !_photos.Any(x => x.Id == _currentId)) _currentId = _photos.FirstOrDefault()?.Id;
                if (_photos.Length == 0) _slideshow = false;
            }
            published = true;
        }
        try
        {
            if (mutation is not null)
            {
                try { await mutation(token).ConfigureAwait(false); }
                catch
                {
                    // A platform operation can commit before its acknowledgement fails.
                    // Reconcile authoritative state so a follow-up query never reports a
                    // deleted file or a favorite value that is no longer persisted.
                    await Reload(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            await Reload(mutation is null ? token : CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
            if (published) Changed?.Invoke();
        }
    }

    public IReadOnlyList<PhotoRecord> Search(string? id, string? keyword, string? category, int limit)
    {
        if ((id?.Length ?? 0) > PhotoRecord.MaximumIdLength) throw new ArgumentException("Photo ID is too long.");
        if (!string.IsNullOrEmpty(category) && category is not ("Photo" or "Tizen.Entity.Photo" or "org.tizen.photogallery"))
            throw new ArgumentException("Category must identify Photo or this gallery.");
        var criteria = PhotoSearchCriteria.Create(keyword, null, null, limit == 0 ? 100 : limit);
        lock (_gate)
        {
            if (!_ready) throw new InvalidOperationException("The media library is loading or unavailable. Retry refresh.");
            return _photos.Where(x => string.IsNullOrEmpty(id) || x.Id == id)
                .Where(x => criteria.Keyword.Length == 0 || x.Title.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase)
                    || x.Album.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase)
                    || x.CapturedAt.ToString("yyyy-MM-dd").Contains(criteria.Keyword, StringComparison.Ordinal))
                .Take(criteria.Limit).ToArray();
        }
    }
    public PhotoResolution Resolve(IReadOnlyList<string> ids)
    {
        if (ids is null || ids.Count == 0) throw new ArgumentException("Provide at least one photo ID.");
        return PhotoResolver.ResolveByIds(Snapshot, ids);
    }
    public PhotoRecord Find(string id) => Resolve([id]).Photos.FirstOrDefault()
        ?? throw new InvalidOperationException("The photo is no longer available.");
    public void Show(string id)
    {
        lock (_gate) { Find(id); _currentId = id; _slideshow = false; }
        Changed?.Invoke();
    }
    public PhotoRecord Current()
    {
        lock (_gate) return _photos.FirstOrDefault(x => x.Id == _currentId)
            ?? throw new InvalidOperationException("No photo is currently displayed.");
    }
    public void StartSlideshow()
    {
        lock (_gate)
        {
            if (_slideshow) throw new InvalidOperationException("A slideshow is already running.");
            if (_photos.Length == 0) throw new InvalidOperationException("There are no photos for a slideshow.");
            _currentId ??= _photos[0].Id;
            _slideshow = true;
        }
        Changed?.Invoke();
    }
    public void StopSlideshow()
    {
        lock (_gate)
        {
            if (!_slideshow) throw new InvalidOperationException("No slideshow is running.");
            _slideshow = false;
        }
        Changed?.Invoke();
    }
    public void Advance(int direction)
    {
        lock (_gate)
        {
            if (_currentId is null || _photos.Length == 0) throw new InvalidOperationException("Open a photo first.");
            var index = Array.FindIndex(_photos, x => x.Id == _currentId);
            _currentId = _photos[(index + direction + _photos.Length) % _photos.Length].Id;
        }
        Changed?.Invoke();
    }
    public void CloseViewer()
    {
        lock (_gate) { _currentId = null; _slideshow = false; }
        Changed?.Invoke();
    }
}
