using Browser.Domain;

namespace Browser.UseCases;

public interface IWebRuntime
{
    Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken);
}

public sealed record WebNavigationOutcome(bool Succeeded, string Title, string Details, string? Error)
{
    public static WebNavigationOutcome Loaded(string title, string details) =>
        new(true, title ?? throw new ArgumentNullException(nameof(title)), details ?? throw new ArgumentNullException(nameof(details)), null);

    public static WebNavigationOutcome Failed(string error) =>
        new(false, string.Empty, string.Empty, string.IsNullOrWhiteSpace(error)
            ? throw new ArgumentException("A failed navigation must include an error.", nameof(error))
            : error);
}

public enum BrowserNavigationStatus
{
    Loaded,
    Failed,
    Superseded
}

public sealed record BrowserNavigationResult(
    BrowserNavigationStatus Status,
    BrowserPage? Page,
    string? Error);

public sealed class BrowserNavigationCoordinator : IAsyncDisposable
{
    private readonly IWebRuntime _webRuntime;
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private long _latestNavigationId;
    private int _disposed;

    public BrowserNavigationCoordinator(IWebRuntime webRuntime)
    {
        _webRuntime = webRuntime ?? throw new ArgumentNullException(nameof(webRuntime));
    }

    public BrowserPage? CurrentPage { get; private set; }

    public async Task<BrowserNavigationResult> NavigateAsync(
        string pageId,
        string url,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Browser navigation URL must be an absolute HTTP or HTTPS URL.", nameof(url));
        }

        var navigationId = Interlocked.Increment(ref _latestNavigationId);
        await _navigationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await _webRuntime.NavigateAsync(uri, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSuperseded(navigationId))
            {
                return new BrowserNavigationResult(BrowserNavigationStatus.Superseded, null, null);
            }

            if (!outcome.Succeeded)
            {
                return new BrowserNavigationResult(BrowserNavigationStatus.Failed, null, outcome.Error);
            }

            var page = BrowserPage.Create(pageId, uri.AbsoluteUri, outcome.Title, outcome.Details);
            CurrentPage = page;
            return new BrowserNavigationResult(BrowserNavigationStatus.Loaded, page, null);
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _navigationGate.WaitAsync().ConfigureAwait(false);
        _navigationGate.Release();
        _navigationGate.Dispose();
    }

    private bool IsSuperseded(long navigationId) => Volatile.Read(ref _latestNavigationId) != navigationId;
}