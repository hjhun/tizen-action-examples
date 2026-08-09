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

        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Browser NUI composition requires a UI synchronization context.");
        _chrome = new BrowserChromeView(
            NavigateAddressFromUi,
            goBack: GoBackFromUi,
            goForward: GoForwardFromUi,
            reload: ReloadFromUi,
            retry: RetryFromUi,
            recoveryBack: HandleBackFromUi);
        Window.Default.GetDefaultLayer().Add(_chrome.Root);
        UpdateReferenceCanvasLayout();
        Window.Default.Resized += OnWindowResized;
        Window.Default.InsetsChanged += OnWindowResized;

        IWebRuntime runtime;
        try
        {
            _webView = NuiWebViewRuntime.CreateSystemWebView();
            _chrome.AddWebView(_webView);
            _webRuntime = new NuiWebViewRuntime(_webView, BrowserNavigationPolicy.NavigationTimeout);
            runtime = _webRuntime;
        }
        catch
        {
            if (_webView is not null)
            {
                _chrome.Canvas.Remove(_webView);
                try
                {
                    _webView.Dispose();
                }
                catch
                {
                    // The engine initialization failure already owns this recovery path.
                }

                _webView = null;
            }

            runtime = new UnavailableWebRuntime();
        }

        _navigation = new BrowserNavigationCoordinator(runtime);
        _navigation.StateChanged += OnNavigationStateChanged;
        _queries = new BrowserPageQueryService(_navigation);

        BrowserActionProviderHost.Start(_queries, new NuiNavigationBridge(this, _uiContext));
        BrowserViewActionProviderHost.Start();
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        Window.Default.KeyEvent += OnKeyEvent;
        _chrome.FocusAddress();
        _ = NavigateFromUiAsync("tab-1", InitialPage.AbsoluteUri);
    }

    protected override void OnTerminate()
    {
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        if (_navigation is not null)
        {
            _navigation.StateChanged -= OnNavigationStateChanged;
        }
        Window.Default.Resized -= OnWindowResized;
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.KeyEvent -= OnKeyEvent;
        _lifetime.Cancel();
        BrowserViewActionProviderHost.ClearPublishedViews();

        if (_webView is not null)
        {
            _chrome?.Canvas.Remove(_webView);
        }

        if (_chrome is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_chrome.Root);
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
            if (eventArgs.Key.KeyPressedName is "XF86Back" or "Escape" or "Back")
            {
                HandleBackFromUi();
            }
            else
            {
                _chrome?.TryHandleKey(eventArgs.Key.KeyPressedName);
            }
        }
    }

    private void UpdateReferenceCanvasLayout()
    {
        if (_chrome is null)
        {
            return;
        }

        var window = Window.Default.WindowSize;
        var insets = Window.Default.GetInsets();
        if (!ReferenceCanvasViewport.TryCreate(
                window.Width,
                window.Height,
                insets.Start,
                insets.Top,
                insets.End,
                insets.Bottom,
                out var viewport))
        {
            // Keep the previous valid frame during a transient resize/inset update.
            return;
        }

        _chrome.UpdatePhysicalSize(window.Width, window.Height);
        _chrome.Canvas.Scale = new Vector3(viewport.Scale, viewport.Scale, 1.0f);
        _chrome.Canvas.Position = new Position(viewport.OffsetX, viewport.OffsetY);
    }

    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs eventArgs) =>
        PublishCurrentPageAnnotation();

    private async Task NavigateFromUiAsync(string pageId, string input)
    {
        if (_navigation is null)
        {
            return;
        }

        try
        {
            await _navigation.NavigateInputAsync(pageId, input, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Application lifecycle ended while an engine request was in flight.
        }
    }

    private void NavigateAddressFromUi(string address)
    {
        _ = NavigateFromUiAsync("tab-1", address);
    }

    private void ReloadFromUi() => _ = RunNavigationCommandAsync(navigation => navigation.ReloadAsync(_lifetime.Token));

    private void RetryFromUi() => _ = RunNavigationCommandAsync(navigation => navigation.RetryAsync(_lifetime.Token));

    private void GoBackFromUi() => _ = RunNavigationCommandAsync(navigation => navigation.GoBackAsync(_lifetime.Token));

    private void GoForwardFromUi() => _ = RunNavigationCommandAsync(navigation => navigation.GoForwardAsync(_lifetime.Token));

    private async Task RunNavigationCommandAsync(Func<BrowserNavigationCoordinator, Task<BrowserNavigationResult>> command)
    {
        if (_navigation is null)
        {
            return;
        }

        try
        {
            await command(_navigation);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Application lifecycle ended while a local command was in flight.
        }
    }

    private void HandleBackFromUi()
    {
        if (_navigation is null)
        {
            Exit();
            return;
        }

        if (_navigation.DismissTransientState())
        {
            _chrome?.FocusAddress();
            return;
        }

        if (_navigation.CurrentState.History.CanGoBack)
        {
            GoBackFromUi();
            return;
        }

        Exit();
    }

    private void OnNavigationStateChanged(object? sender, BrowserNavigationState state)
    {
        var context = _uiContext;
        if (context is null || _lifetime.IsCancellationRequested)
        {
            return;
        }

        if (SynchronizationContext.Current == context)
        {
            ApplyNavigationState(state);
        }
        else
        {
            context.Post(static value =>
            {
                var update = (NavigationStateUpdate)value!;
                if (!update.Application._lifetime.IsCancellationRequested)
                {
                    update.Application.ApplyNavigationState(update.State);
                }
            }, new NavigationStateUpdate(this, state));
        }
    }

    private void ApplyNavigationState(BrowserNavigationState state)
    {
        _chrome?.UpdateNavigationState(state);
        PublishCurrentPageAnnotation();
    }

    private void PublishCurrentPageAnnotation()
    {
        if (_webView is null || _navigation?.CurrentState is not { Phase: BrowserNavigationPhase.Page, Page: { } page } ||
            _lifetime.IsCancellationRequested)
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
                        _ = request.Application.NavigateFromUiAsync(request.Page.Id, request.Page.Url);
                    }
                },
                new NavigationRequest(_application, page));
            return true;
        }

        private sealed record NavigationRequest(BrowserApplication Application, BrowserPage Page);
    }

    private sealed record NavigationStateUpdate(BrowserApplication Application, BrowserNavigationState State);

    private sealed class UnavailableWebRuntime : IWebRuntime
    {
        public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
            Task.FromResult(WebNavigationOutcome.Failed(
                WebNavigationFailure.EngineUnavailable,
                "The system WebView is unavailable. Try again after restarting the app."));
    }

    private static void Main(string[] args)
    {
        var app = new BrowserApplication();
        app.Run(args);
    }
}
