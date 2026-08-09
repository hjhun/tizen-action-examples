using Browser.ActionProvider;
using Browser.Domain;
using Browser.UseCases;
using Browser.ViewActionProvider;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Browser.App;

/// <summary>
/// Owns the NUI WebView, shared navigation/query services, and current-page annotation publication.
/// </summary>
internal sealed class BrowserApplication : NUIApplication
{
    private static readonly Uri InitialPage = new("https://www.tizen.org/");
    private readonly CancellationTokenSource _lifetime = new();
    private BrowserChromeView? _chrome;
    private WebView? _webView;
    private NuiWebViewRuntime? _webRuntime;
    private BrowserNavigationCoordinator? _navigation;
    private BrowserPageQueryService? _queries;
    private SynchronizationContext? _uiContext;

    protected override void OnCreate()
    {
        base.OnCreate();

        _chrome = new BrowserChromeView(NavigateAddressFromUi);
        Window.Default.GetDefaultLayer().Add(_chrome.Canvas);
        UpdateReferenceCanvasLayout();
        _webView = NuiWebViewRuntime.CreateSystemWebView();
        _chrome.AddWebView(_webView);
        Window.Default.Resized += OnWindowResized;

        _webRuntime = new NuiWebViewRuntime(_webView, TimeSpan.FromMinutes(2));
        _navigation = new BrowserNavigationCoordinator(_webRuntime);
        _queries = new BrowserPageQueryService(_navigation);
        _uiContext = SynchronizationContext.Current;
        if (_uiContext is null)
        {
            throw new InvalidOperationException("Browser NUI composition requires a UI synchronization context.");
        }

        BrowserActionProviderHost.Start(_queries, new NuiNavigationBridge(this, _uiContext));
        BrowserViewActionProviderHost.Start();
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        Window.Default.KeyEvent += OnKeyEvent;
        _chrome.FocusAddress();
        _ = NavigateFromUiAsync(InitialPage);
    }

    protected override void OnTerminate()
    {
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        Window.Default.Resized -= OnWindowResized;
        Window.Default.KeyEvent -= OnKeyEvent;
        _lifetime.Cancel();
        BrowserViewActionProviderHost.ClearPublishedViews();

        if (_webView is not null)
        {
            _chrome?.Canvas.Remove(_webView);
        }

        if (_chrome is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_chrome.Canvas);
        }

        _ = DisposeNavigationAsync();
        _lifetime.Dispose();
        base.OnTerminate();
    }

    private void OnWindowResized(object? sender, EventArgs eventArgs)
    {
        UpdateReferenceCanvasLayout();
        PublishCurrentPageAnnotation();
    }

    private void OnKeyEvent(object? sender, Window.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key.State == Key.StateType.Down)
        {
            _chrome?.TryHandleKey(eventArgs.Key.KeyPressedName);
        }
    }

    private void UpdateReferenceCanvasLayout()
    {
        if (_chrome is null)
        {
            return;
        }

        var window = Window.Default.WindowSize;
        var scale = MathF.Min(window.Width / BrowserChromeView.DesignWidth, window.Height / BrowserChromeView.DesignHeight);
        if (!float.IsFinite(scale) || scale <= 0)
        {
            return;
        }

        _chrome.Canvas.Scale = new Vector3(scale, scale, 1.0f);
        _chrome.Canvas.Position = new Position(
            (window.Width - (BrowserChromeView.DesignWidth * scale)) / 2.0f,
            (window.Height - (BrowserChromeView.DesignHeight * scale)) / 2.0f);
    }

    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs eventArgs) =>
        PublishCurrentPageAnnotation();

    private async Task NavigateFromUiAsync(Uri uri)
    {
        if (_navigation is null)
        {
            return;
        }

        try
        {
            var result = await _navigation.NavigateAsync("initial-page", uri.AbsoluteUri, _lifetime.Token);
            _chrome?.UpdatePage(result.Page, result.Error);
            PublishCurrentPageAnnotation();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Application lifecycle ended while an engine request was in flight.
        }
    }

    private void NavigateAddressFromUi(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _ = NavigateFromUiAsync(uri);
        }
        else
        {
            _chrome?.UpdatePage(null, "Enter a complete http:// or https:// address.");
        }
    }

    private void PublishCurrentPageAnnotation()
    {
        if (_webView is null || _navigation?.CurrentPage is not { } page || _lifetime.IsCancellationRequested)
        {
            BrowserViewActionProviderHost.ClearPublishedViews();
            return;
        }

        try
        {
            var bounds = _webView.CalculateScreenPositionSize();
            if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) ||
                !float.IsFinite(bounds.Z) || !float.IsFinite(bounds.W) ||
                bounds.Z <= 0 || bounds.W <= 0)
            {
                BrowserViewActionProviderHost.ClearPublishedViews();
                return;
            }

            double? windowX = null;
            double? windowY = null;
            try
            {
                using var windowPosition = Window.Default.WindowPosition;
                windowX = bounds.X - windowPosition.X;
                windowY = bounds.Y - windowPosition.Y;
            }
            catch
            {
                // Screen bounds remain usable when the platform does not expose window position.
            }

            var focused = ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _webView);
            BrowserViewActionProviderHost.PublishVisiblePage(new BrowserPageViewSnapshot(
                page, bounds.X, bounds.Y, windowX, windowY, bounds.Z, bounds.W, focused));
        }
        catch
        {
            // A transient NUI layout/lifecycle frame is never published as stale annotation data.
            BrowserViewActionProviderHost.ClearPublishedViews();
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

    private sealed class NuiNavigationBridge : IBrowserActionNavigation
    {
        private readonly BrowserApplication _application;
        private readonly SynchronizationContext _uiContext;

        public NuiNavigationBridge(BrowserApplication application, SynchronizationContext uiContext)
        {
            _application = application;
            _uiContext = uiContext;
        }

        public bool RequestNavigation(BrowserPage page)
        {
            ArgumentNullException.ThrowIfNull(page);
            if (_application._lifetime.IsCancellationRequested)
            {
                return false;
            }

            _uiContext.Post(
                static state =>
                {
                    var request = (NavigationRequest)state!;
                    if (!request.Application._lifetime.IsCancellationRequested)
                    {
                        _ = request.Application.NavigateFromUiAsync(new Uri(request.Page.Url, UriKind.Absolute));
                    }
                },
                new NavigationRequest(_application, page));
            return true;
        }

        private sealed record NavigationRequest(BrowserApplication Application, BrowserPage Page);
    }

    private static void Main(string[] args)
    {
        var app = new BrowserApplication();
        app.Run(args);
    }
}
