using Browser.UseCases;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Browser.App;

/// <summary>
/// Owns the NUI WebView and routes UI-originated URL navigation through the shared use case.
/// Provider composition is intentionally deferred until generated provider bindings are added.
/// </summary>
internal sealed class BrowserApplication : NUIApplication
{
    private static readonly Uri InitialPage = new("https://www.tizen.org/");
    private readonly CancellationTokenSource _lifetime = new();
    private WebView? _webView;
    private NuiWebViewRuntime? _webRuntime;
    private BrowserNavigationCoordinator? _navigation;

    protected override void OnCreate()
    {
        base.OnCreate();

        _webView = NuiWebViewRuntime.CreateSystemWebView();
        _webView.Size = Window.Default.WindowSize;
        Window.Default.GetDefaultLayer().Add(_webView);
        Window.Default.Resized += OnWindowResized;

        _webRuntime = new NuiWebViewRuntime(_webView, TimeSpan.FromMinutes(2));
        _navigation = new BrowserNavigationCoordinator(_webRuntime);
        _ = NavigateFromUiAsync(InitialPage);
    }

    protected override void OnTerminate()
    {
        Window.Default.Resized -= OnWindowResized;
        _lifetime.Cancel();

        if (_webView is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_webView);
        }

        _ = DisposeNavigationAsync();
        _lifetime.Dispose();
        base.OnTerminate();
    }

    private void OnWindowResized(object? sender, EventArgs eventArgs)
    {
        if (_webView is not null)
        {
            _webView.Size = Window.Default.WindowSize;
        }
    }

    private async Task NavigateFromUiAsync(Uri uri)
    {
        if (_navigation is null)
        {
            return;
        }

        try
        {
            await _navigation.NavigateAsync("initial-page", uri.AbsoluteUri, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Application lifecycle ended while an engine request was in flight.
        }
    }

    private async Task DisposeNavigationAsync()
    {
        if (_navigation is not null)
        {
            await _navigation.DisposeAsync();
        }

        if (_webRuntime is not null)
        {
            await _webRuntime.DisposeAsync();
        }
    }

    private static void Main(string[] args)
    {
        var app = new BrowserApplication();
        app.Run(args);
    }
}
