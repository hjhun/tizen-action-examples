using Browser.UseCases;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Browser.App;

/// <summary>
/// Bridges the target WebView to the portable navigation use case. The runtime captures the NUI
/// synchronization context at construction and marshals all WebView work back to that UI thread.
/// </summary>
public sealed class NuiWebViewRuntime : IWebRuntime, IAsyncDisposable
{
    private const int MaximumTitleLength = 512;
    private const int MaximumErrorLength = 512;
    private readonly WebView _webView;
    private readonly TimeSpan _navigationTimeout;
    private readonly SynchronizationContext _nuiSynchronizationContext;
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private int _disposed;

    public NuiWebViewRuntime(WebView webView, TimeSpan navigationTimeout)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        if (navigationTimeout <= TimeSpan.Zero || navigationTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(navigationTimeout));
        }

        _navigationTimeout = navigationTimeout;
        _nuiSynchronizationContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("NuiWebViewRuntime must be created on the NUI UI thread.");
    }

    public static WebView CreateSystemWebView() =>
        new(["org.tizen.browser"], WebView.WebEngineType.UseSystemSetting);

    public async Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("WebView navigation requires an absolute HTTP or HTTPS URI.", nameof(uri));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _navigationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var completion = new TaskCompletionSource<WebNavigationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pageLoadStarted = 0;
            var stopRequested = 0;

            EventHandler<WebViewPageLoadEventArgs> started = (_, _) => Volatile.Write(ref pageLoadStarted, 1);
            EventHandler<WebViewPageLoadEventArgs> finished = (_, _) =>
            {
                if (Volatile.Read(ref pageLoadStarted) != 0)
                {
                    completion.TrySetResult(WebNavigationOutcome.Loaded(
                        Limit(_webView.Title, MaximumTitleLength),
                        "Loaded in the system WebView."));
                }
            };
            EventHandler<WebViewPageLoadErrorEventArgs> failed = (_, eventArgs) =>
                completion.TrySetResult(WebNavigationOutcome.Failed(
                    Limit(eventArgs.PageLoadError?.Description, MaximumErrorLength, "The WebView could not load the page.")));

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                Interlocked.Exchange(ref stopRequested, 1);
                completion.TrySetCanceled(cancellationToken);
            });
            using var timeout = new CancellationTokenSource(_navigationTimeout);
            using var timeoutRegistration = timeout.Token.Register(() =>
            {
                Interlocked.Exchange(ref stopRequested, 1);
                completion.TrySetResult(WebNavigationOutcome.Failed("The WebView navigation timed out."));
            });

            try
            {
                await InvokeOnNuiThreadAsync(() =>
                {
                    _webView.PageLoadStarted += started;
                    _webView.PageLoadFinished += finished;
                    _webView.PageLoadError += failed;
                    _webView.LoadUrl(uri.AbsoluteUri);
                }).ConfigureAwait(false);
                return await completion.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return WebNavigationOutcome.Failed(Limit(exception.Message, MaximumErrorLength, "The WebView could not start navigation."));
            }
            finally
            {
                await InvokeOnNuiThreadAsync(() =>
                {
                    if (Volatile.Read(ref stopRequested) != 0)
                    {
                        TryStopLoading();
                    }

                    _webView.PageLoadStarted -= started;
                    _webView.PageLoadFinished -= finished;
                    _webView.PageLoadError -= failed;
                }).ConfigureAwait(false);
            }
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
        try
        {
            await InvokeOnNuiThreadAsync(_webView.Dispose).ConfigureAwait(false);
        }
        finally
        {
            _navigationGate.Release();
            _navigationGate.Dispose();
        }
    }

    private void TryStopLoading()
    {
        try
        {
            _webView.StopLoading();
        }
        catch (ObjectDisposedException)
        {
            // Disposal wins over a late cancellation/timeout callback.
        }
    }

    private Task InvokeOnNuiThreadAsync(Action action)
    {
        if (SynchronizationContext.Current == _nuiSynchronizationContext)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _nuiSynchronizationContext.Post(_ =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }, null);
        return completion.Task;
    }

    private static string Limit(string? value, int maximumLength, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
