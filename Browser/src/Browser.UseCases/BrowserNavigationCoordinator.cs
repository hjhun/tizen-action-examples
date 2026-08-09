using Browser.Domain;

namespace Browser.UseCases;

public static class BrowserNavigationPolicy
{
    public static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(15);
}

public interface IWebRuntime
{
    Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken);

    Task<WebNavigationOutcome> ReloadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WebNavigationOutcome.Failed(WebNavigationFailure.EngineUnavailable, "Reload is unavailable."));

    Task<WebNavigationOutcome> GoBackAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WebNavigationOutcome.Failed(WebNavigationFailure.EngineUnavailable, "Back navigation is unavailable."));

    Task<WebNavigationOutcome> GoForwardAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WebNavigationOutcome.Failed(WebNavigationFailure.EngineUnavailable, "Forward navigation is unavailable."));
}

public readonly record struct WebHistoryAvailability(bool CanGoBack, bool CanGoForward);

public enum WebNavigationFailure
{
    Network,
    EngineUnavailable,
    Timeout,
    InvalidInput,
}

public sealed record WebNavigationOutcome(
    bool Succeeded,
    string Title,
    string Details,
    Uri? EffectiveUri,
    WebHistoryAvailability History,
    WebNavigationFailure? Failure,
    string? Error)
{
    public static WebNavigationOutcome Loaded(
        string title,
        string details,
        Uri? effectiveUri = null,
        WebHistoryAvailability history = default) =>
        new(
            true,
            title ?? throw new ArgumentNullException(nameof(title)),
            details ?? throw new ArgumentNullException(nameof(details)),
            effectiveUri,
            history,
            null,
            null);

    public static WebNavigationOutcome Failed(string error) => Failed(WebNavigationFailure.Network, error);

    public static WebNavigationOutcome Failed(WebNavigationFailure failure, string error) =>
        new(
            false,
            string.Empty,
            string.Empty,
            null,
            default,
            failure,
            string.IsNullOrWhiteSpace(error)
                ? throw new ArgumentException("A failed navigation must include an error.", nameof(error))
                : error);
}

public enum BrowserNavigationStatus
{
    Loaded,
    Failed,
    Superseded,
    Unavailable,
}

public enum BrowserNavigationPhase
{
    Home,
    Loading,
    Page,
    Offline,
    EngineError,
    Timeout,
    InvalidInput,
}

public sealed record BrowserNavigationState(
    long IntentId,
    BrowserNavigationPhase Phase,
    BrowserPage? Page,
    string? PublicUrl,
    string? Error,
    WebHistoryAvailability History)
{
    public static BrowserNavigationState Initial { get; } =
        new(0, BrowserNavigationPhase.Home, null, null, null, default);
}

public sealed record BrowserNavigationResult(
    BrowserNavigationStatus Status,
    BrowserPage? Page,
    string? Error);

public sealed record BrowserNavigationRequest(Uri NavigationUri, string PublicDisplayUri, bool IsSearch);

public static class BrowserNavigationInput
{
    private const int MaximumAddressLength = 4_096;
    private const int MaximumSearchLength = 512;
    private const string SearchEndpoint = "https://duckduckgo.com/";

    public static bool TryNormalize(
        string? rawInput,
        out BrowserNavigationRequest request,
        out string? error)
    {
        request = default!;
        error = null;
        var input = rawInput?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter an address or search phrase.";
            return false;
        }

        if (input.Length > MaximumAddressLength)
        {
            error = "The address or search phrase is too long.";
            return false;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "Use an HTTP or HTTPS address without embedded credentials.";
                return false;
            }

            request = new BrowserNavigationRequest(uri, ProjectPublicUri(uri), false);
            return true;
        }

        if (HasExplicitScheme(input))
        {
            error = "Enter a valid HTTP or HTTPS address.";
            return false;
        }

        if (input.Length > MaximumSearchLength)
        {
            error = "The search phrase is too long.";
            return false;
        }

        var searchUri = new Uri($"{SearchEndpoint}?q={Uri.EscapeDataString(input)}", UriKind.Absolute);
        request = new BrowserNavigationRequest(searchUri, SearchEndpoint, true);
        return true;
    }

    private static bool HasExplicitScheme(string input)
    {
        var separator = input.IndexOf(':');
        if (separator <= 0 || !char.IsAsciiLetter(input[0]))
        {
            return false;
        }

        return input[1..separator].All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.');
    }

    public static string ProjectPublicUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty,
        };
        return builder.Uri.AbsoluteUri;
    }
}

public sealed class BrowserNavigationCoordinator : IAsyncDisposable
{
    private const int MaximumErrorLength = 256;
    private readonly IWebRuntime _webRuntime;
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private readonly object _activeRequestLock = new();
    private CancellationTokenSource? _activeRequest;
    private BrowserNavigationState _currentState = BrowserNavigationState.Initial;
    private BrowserPage? _currentPage;
    private BrowserNavigationRequest? _lastRequest;
    private string? _lastPageId;
    private long _latestNavigationId;
    private int _disposed;

    public BrowserNavigationCoordinator(IWebRuntime webRuntime)
    {
        _webRuntime = webRuntime ?? throw new ArgumentNullException(nameof(webRuntime));
    }

    public event EventHandler<BrowserNavigationState>? StateChanged;

    public BrowserPage? CurrentPage => Volatile.Read(ref _currentPage);

    public BrowserNavigationState CurrentState => Volatile.Read(ref _currentState);

    public Task<BrowserNavigationResult> NavigateInputAsync(
        string pageId,
        string? input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!BrowserNavigationInput.TryNormalize(input, out var request, out var error))
        {
            return Task.FromResult(PublishInvalidInput(error!));
        }

        return NavigateRequestAsync(pageId, request, cancellationToken);
    }

    public Task<BrowserNavigationResult> NavigateAsync(
        string pageId,
        string url,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (!BrowserNavigationInput.TryNormalize(url, out var request, out _) || request.IsSearch)
        {
            throw new ArgumentException("Browser navigation URL must be an absolute HTTP or HTTPS URL without credentials.", nameof(url));
        }

        return NavigateRequestAsync(pageId, request, cancellationToken);
    }

    public Task<BrowserNavigationResult> ReloadAsync(CancellationToken cancellationToken)
    {
        var current = CurrentPage;
        if (current is null)
        {
            return Task.FromResult(Unavailable("There is no page to reload."));
        }

        return ExecuteAsync(
            current.Id,
            new Uri(current.Url, UriKind.Absolute),
            _webRuntime.ReloadAsync,
            cancellationToken);
    }

    public Task<BrowserNavigationResult> GoBackAsync(CancellationToken cancellationToken)
    {
        var current = CurrentPage;
        if (current is null || !CurrentState.History.CanGoBack)
        {
            return Task.FromResult(Unavailable("Back navigation is unavailable."));
        }

        return ExecuteAsync(
            current.Id,
            new Uri(current.Url, UriKind.Absolute),
            _webRuntime.GoBackAsync,
            cancellationToken);
    }

    public Task<BrowserNavigationResult> GoForwardAsync(CancellationToken cancellationToken)
    {
        var current = CurrentPage;
        if (current is null || !CurrentState.History.CanGoForward)
        {
            return Task.FromResult(Unavailable("Forward navigation is unavailable."));
        }

        return ExecuteAsync(
            current.Id,
            new Uri(current.Url, UriKind.Absolute),
            _webRuntime.GoForwardAsync,
            cancellationToken);
    }

    public Task<BrowserNavigationResult> RetryAsync(CancellationToken cancellationToken)
    {
        BrowserNavigationRequest? request;
        string? pageId;
        lock (_activeRequestLock)
        {
            request = _lastRequest;
            pageId = _lastPageId;
        }

        if (request is null || string.IsNullOrEmpty(pageId))
        {
            return Task.FromResult(Unavailable("There is no navigation to retry."));
        }

        return NavigateRequestAsync(pageId, request, cancellationToken);
    }

    public bool DismissTransientState()
    {
        var state = CurrentState;
        if (state.Phase is not (BrowserNavigationPhase.Offline or BrowserNavigationPhase.EngineError or
            BrowserNavigationPhase.Timeout or BrowserNavigationPhase.InvalidInput))
        {
            return false;
        }

        var current = CurrentPage;
        Publish(current is null
            ? BrowserNavigationState.Initial with { IntentId = state.IntentId }
            : new BrowserNavigationState(state.IntentId, BrowserNavigationPhase.Page, current, current.Url, null, state.History));
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CancellationTokenSource? active;
        lock (_activeRequestLock)
        {
            active = _activeRequest;
        }

        active?.Cancel();
        await _navigationGate.WaitAsync().ConfigureAwait(false);
        _navigationGate.Release();
        _navigationGate.Dispose();
    }

    private Task<BrowserNavigationResult> NavigateRequestAsync(
        string pageId,
        BrowserNavigationRequest request,
        CancellationToken cancellationToken)
    {
        lock (_activeRequestLock)
        {
            _lastPageId = pageId;
            _lastRequest = request;
        }
        return ExecuteAsync(pageId, request.NavigationUri, token => _webRuntime.NavigateAsync(request.NavigationUri, token), cancellationToken);
    }

    private async Task<BrowserNavigationResult> ExecuteAsync(
        string pageId,
        Uri requestedUri,
        Func<CancellationToken, Task<WebNavigationOutcome>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var navigationId = Interlocked.Increment(ref _latestNavigationId);
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource? previous;
        lock (_activeRequestLock)
        {
            previous = _activeRequest;
            _activeRequest = requestCancellation;
        }

        previous?.Cancel();
        Publish(new BrowserNavigationState(
            navigationId,
            BrowserNavigationPhase.Loading,
            CurrentPage,
            BrowserNavigationInput.ProjectPublicUri(requestedUri),
            null,
            CurrentState.History));

        var gateHeld = false;
        try
        {
            await _navigationGate.WaitAsync(requestCancellation.Token).ConfigureAwait(false);
            gateHeld = true;
            var outcome = await operation(requestCancellation.Token).ConfigureAwait(false);
            requestCancellation.Token.ThrowIfCancellationRequested();
            if (IsSuperseded(navigationId))
            {
                return new BrowserNavigationResult(BrowserNavigationStatus.Superseded, null, null);
            }

            if (!outcome.Succeeded)
            {
                var error = Limit(outcome.Error, "The WebView could not load the page.");
                Publish(new BrowserNavigationState(
                    navigationId,
                    PhaseFor(outcome.Failure),
                    CurrentPage,
                    BrowserNavigationInput.ProjectPublicUri(requestedUri),
                    error,
                    CurrentState.History));
                return new BrowserNavigationResult(BrowserNavigationStatus.Failed, null, error);
            }

            var effectiveUri = outcome.EffectiveUri ?? requestedUri;
            var publicUrl = BrowserNavigationInput.ProjectPublicUri(effectiveUri);
            var page = BrowserPage.Create(pageId, publicUrl, outcome.Title, outcome.Details);
            Volatile.Write(ref _currentPage, page);
            Publish(new BrowserNavigationState(
                navigationId,
                BrowserNavigationPhase.Page,
                page,
                publicUrl,
                null,
                outcome.History));
            return new BrowserNavigationResult(BrowserNavigationStatus.Loaded, page, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                                                 (IsSuperseded(navigationId) || Volatile.Read(ref _disposed) != 0))
        {
            return new BrowserNavigationResult(BrowserNavigationStatus.Superseded, null, null);
        }
        finally
        {
            if (gateHeld)
            {
                _navigationGate.Release();
            }

            lock (_activeRequestLock)
            {
                if (ReferenceEquals(_activeRequest, requestCancellation))
                {
                    _activeRequest = null;
                }
            }

            requestCancellation.Dispose();
        }
    }

    private BrowserNavigationResult PublishInvalidInput(string error)
    {
        var navigationId = Interlocked.Increment(ref _latestNavigationId);
        CancellationTokenSource? previous;
        lock (_activeRequestLock)
        {
            previous = _activeRequest;
            _activeRequest = null;
        }

        previous?.Cancel();
        var boundedError = Limit(error, "Enter an address or search phrase.");
        Publish(new BrowserNavigationState(
            navigationId,
            BrowserNavigationPhase.InvalidInput,
            CurrentPage,
            null,
            boundedError,
            CurrentState.History));
        return new BrowserNavigationResult(BrowserNavigationStatus.Failed, null, boundedError);
    }

    private static BrowserNavigationPhase PhaseFor(WebNavigationFailure? failure) => failure switch
    {
        WebNavigationFailure.EngineUnavailable => BrowserNavigationPhase.EngineError,
        WebNavigationFailure.Timeout => BrowserNavigationPhase.Timeout,
        WebNavigationFailure.InvalidInput => BrowserNavigationPhase.InvalidInput,
        _ => BrowserNavigationPhase.Offline,
    };

    private static BrowserNavigationResult Unavailable(string error) =>
        new(BrowserNavigationStatus.Unavailable, null, error);

    private void Publish(BrowserNavigationState state)
    {
        Volatile.Write(ref _currentState, state);
        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<BrowserNavigationState> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, state);
            }
            catch
            {
                // A presentation observer cannot break navigation or another observer.
            }
        }
    }

    private bool IsSuperseded(long navigationId) => Volatile.Read(ref _latestNavigationId) != navigationId;

    private static string Limit(string? value, string fallback)
    {
        var selected = string.IsNullOrWhiteSpace(value) ? fallback : value;
        return selected.Length <= MaximumErrorLength ? selected : selected[..MaximumErrorLength];
    }
}
