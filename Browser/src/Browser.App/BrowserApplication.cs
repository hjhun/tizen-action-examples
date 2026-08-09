using Browser.ActionProvider;
using Browser.Domain;
using Browser.Persistence;
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
    private readonly CancellationTokenSource _lifetime = new();
    private BrowserChromeView? _chrome;
    private WebView? _webView;
    private NuiWebViewRuntime? _webRuntime;
    private BrowserNavigationCoordinator? _navigation;
    private BrowserTabCoordinator? _tabsCoordinator;
    private BrowserSessionCoordinator? _sessionCoordinator;
    private BrowserTabPersistenceCoordinator? _tabPersistence;
    private BrowserPageQueryService? _queries;
    private SynchronizationContext? _uiContext;
    private int _paused;
    private int _tabMutationPending;

    protected override void OnCreate()
    {
        base.OnCreate();

        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Browser NUI composition requires a UI synchronization context.");
        _tabsCoordinator = new BrowserTabCoordinator(BrowserTabWorkspace.Create("tab-1"));
        var sessionPath = System.IO.Path.Combine(
            Tizen.Applications.Application.Current.DirectoryInfo.Data,
            "browser-session.json");
        _sessionCoordinator = new BrowserSessionCoordinator(new BrowserFileSessionStore(sessionPath));
        _tabPersistence = new BrowserTabPersistenceCoordinator(_tabsCoordinator, _sessionCoordinator);
        _chrome = new BrowserChromeView(
            NavigateAddressFromUi,
            goBack: GoBackFromUi,
            goForward: GoForwardFromUi,
            openTabs: OpenTabsFromUi,
            reload: ReloadFromUi,
            retry: RetryFromUi,
            recoveryBack: HandleBackFromUi,
            createTab: CreateTabFromUi,
            selectTab: SelectTabFromUi,
            requestClose: RequestCloseTabFromUi,
            confirmClose: ConfirmCloseTabFromUi,
            cancelClose: CancelCloseTabFromUi);
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
        _tabsCoordinator.StateChanged += OnTabStateChanged;
        _queries = new BrowserPageQueryService(_navigation);

        BrowserActionProviderHost.Start(_queries, new NuiNavigationBridge(this, _uiContext));
        BrowserViewActionProviderHost.Start();
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        Window.Default.KeyEvent += OnKeyEvent;
        _chrome.UpdateNavigationState(_navigation.CurrentState);
        _chrome.UpdateWorkspace(_tabsCoordinator.Current);
        _chrome.FocusAddress();
        _ = RestoreSessionAsync();
    }

    protected override void OnTerminate()
    {
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        if (_navigation is not null)
        {
            _navigation.StateChanged -= OnNavigationStateChanged;
        }
        if (_tabsCoordinator is not null)
        {
            _tabsCoordinator.StateChanged -= OnTabStateChanged;
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

    protected override void OnPause()
    {
        Volatile.Write(ref _paused, 1);
        SaveSessionFromUi();
        BrowserViewActionProviderHost.ClearPublishedViews();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Volatile.Write(ref _paused, 0);
        UpdateReferenceCanvasLayout();
        if (_navigation is not null)
        {
            _chrome?.UpdateNavigationState(_navigation.CurrentState);
        }

        if (_tabsCoordinator is not null)
        {
            _chrome?.UpdateWorkspace(_tabsCoordinator.Current);
        }

        PublishCurrentPageAnnotation();
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
        var tabId = _tabsCoordinator?.Current.SelectedTabId;
        if (!string.IsNullOrEmpty(tabId))
        {
            _ = NavigateFromUiAsync(tabId, address);
        }
    }

    private void OpenTabsFromUi()
    {
        if (Volatile.Read(ref _tabMutationPending) == 0)
        {
            _tabsCoordinator?.OpenTabs();
        }
    }

    private void CreateTabFromUi()
    {
        _ = CreateTabPersistedAsync();
    }

    private void SelectTabFromUi(string tabId)
    {
        _ = SelectTabPersistedAsync(tabId);
    }

    private async Task CreateTabPersistedAsync()
    {
        if (_tabPersistence is null || Interlocked.CompareExchange(ref _tabMutationPending, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _chrome?.SetTabMutationBusy(true);
            await _tabPersistence.CreateTabAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Lifecycle ended before the desired tab could become durable.
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Persist-first semantics leave the prior tab workspace published.
        }
        finally
        {
            Volatile.Write(ref _tabMutationPending, 0);
            _chrome?.SetTabMutationBusy(false);
        }
    }

    private async Task SelectTabPersistedAsync(string tabId)
    {
        if (_tabPersistence is null || Interlocked.CompareExchange(ref _tabMutationPending, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _chrome?.SetTabMutationBusy(true);
            var result = await _tabPersistence.SelectTabAsync(tabId, _lifetime.Token);
            if (result.Succeeded && result.SelectedTab is not null)
            {
                ActivateTab(result.SelectedTab);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Lifecycle ended before selection could become durable.
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Persist-first semantics leave the prior selection published.
        }
        finally
        {
            Volatile.Write(ref _tabMutationPending, 0);
            _chrome?.SetTabMutationBusy(false);
        }
    }

    private void RequestCloseTabFromUi(string tabId)
    {
        if (Volatile.Read(ref _tabMutationPending) == 0)
        {
            _tabsCoordinator?.TryRequestClose(tabId);
        }
    }

    private void CancelCloseTabFromUi()
    {
        if (Volatile.Read(ref _tabMutationPending) == 0)
        {
            _tabsCoordinator?.TryCancelClose();
        }
    }

    private void ConfirmCloseTabFromUi()
    {
        _ = ConfirmClosePersistedAsync();
    }

    private async Task ConfirmClosePersistedAsync()
    {
        if (_tabPersistence is null || _tabsCoordinator is null ||
            Interlocked.CompareExchange(ref _tabMutationPending, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _chrome?.SetTabMutationBusy(true);
            if (await _tabPersistence.ConfirmCloseAsync(_lifetime.Token))
            {
                ActivateTab(_tabsCoordinator.Current.SelectedTab, keepTabsOpen: true);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Lifecycle ended before close confirmation could commit.
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Persist-first semantics leave the tab and modal state unchanged.
        }
        finally
        {
            Volatile.Write(ref _tabMutationPending, 0);
            _chrome?.SetTabMutationBusy(false);
        }
    }

    private void ActivateTab(BrowserTab tab, bool keepTabsOpen = false)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.Page is null)
        {
            _navigation?.ResetToHome();
        }
        else
        {
            _ = NavigateFromUiAsync(tab.Id, tab.Page.Url);
        }

        if (!keepTabsOpen)
        {
            _chrome?.FocusAddress();
        }
    }

    private async Task RestoreSessionAsync()
    {
        if (_sessionCoordinator is null || _tabsCoordinator is null)
        {
            return;
        }

        BrowserSessionRestoreResult result;
        try
        {
            result = await _sessionCoordinator.RestoreAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }

        var context = _uiContext;
        if (context is null || _lifetime.IsCancellationRequested)
        {
            return;
        }

        context.Post(static value =>
        {
            var restore = (SessionRestoreUpdate)value!;
            if (restore.Application._lifetime.IsCancellationRequested || restore.Application._tabsCoordinator is null)
            {
                return;
            }

            if (restore.Result.Status == BrowserSessionRestoreStatus.Restored && restore.Result.Snapshot is not null)
            {
                restore.Application._tabsCoordinator.Restore(restore.Result.Snapshot);
            }
            else if (restore.Result.Status == BrowserSessionRestoreStatus.InvalidSession)
            {
                restore.Application.SaveSessionFromUi();
            }

            restore.Application.ActivateTab(restore.Application._tabsCoordinator.Current.SelectedTab);
        }, new SessionRestoreUpdate(this, result));
    }

    private void SaveSessionFromUi()
    {
        if (_tabPersistence is null || _lifetime.IsCancellationRequested)
        {
            return;
        }

        _ = SaveCurrentSessionAsync();
    }

    private async Task SaveCurrentSessionAsync()
    {
        if (_tabPersistence is null)
        {
            return;
        }

        try
        {
            await _tabPersistence.SaveCurrentAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Lifecycle cancellation leaves the last complete atomic snapshot intact.
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // The in-memory normal session remains usable when persistence is unavailable.
        }
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
        if (Volatile.Read(ref _tabMutationPending) != 0)
        {
            return;
        }

        var workspace = _tabsCoordinator?.Current;
        if (workspace?.Surface == BrowserWorkspaceSurface.CloseConfirmation)
        {
            CancelCloseTabFromUi();
            return;
        }

        if (workspace?.Surface == BrowserWorkspaceSurface.Tabs)
        {
            _tabsCoordinator?.TryCloseTabs();
            return;
        }

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
        if (state.Phase == BrowserNavigationPhase.Page && state.Page is not null &&
            _tabsCoordinator?.Current.SelectedTabId == state.Page.Id)
        {
            _ = PersistSelectedPageAsync(state.Page);
        }

        _chrome?.UpdateNavigationState(state);
        PublishCurrentPageAnnotation();
    }

    private async Task PersistSelectedPageAsync(BrowserPage page)
    {
        if (_tabPersistence is null)
        {
            return;
        }

        try
        {
            await _tabPersistence.UpdateSelectedPageAsync(page, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Navigation remains visible; a later successful mutation/pause can retry persistence.
        }
    }

    private void OnTabStateChanged(object? sender, BrowserTabWorkspace workspace)
    {
        var context = _uiContext;
        if (context is null || _lifetime.IsCancellationRequested)
        {
            return;
        }

        if (SynchronizationContext.Current == context)
        {
            ApplyWorkspaceState(workspace);
        }
        else
        {
            context.Post(static value =>
            {
                var update = (WorkspaceStateUpdate)value!;
                if (!update.Application._lifetime.IsCancellationRequested)
                {
                    update.Application.ApplyWorkspaceState(update.Workspace);
                }
            }, new WorkspaceStateUpdate(this, workspace));
        }
    }

    private void ApplyWorkspaceState(BrowserTabWorkspace workspace)
    {
        _chrome?.UpdateWorkspace(workspace);
        PublishCurrentPageAnnotation();
    }

    private void PublishCurrentPageAnnotation()
    {
        if (_tabsCoordinator?.Current.Surface != BrowserWorkspaceSurface.Page ||
            _webView is null || _navigation?.CurrentState is not { Phase: BrowserNavigationPhase.Page, Page: { } page } ||
            _lifetime.IsCancellationRequested || Volatile.Read(ref _paused) != 0)
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

        if (_sessionCoordinator is not null)
        {
            await _sessionCoordinator.DisposeAsync();
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

    private sealed record WorkspaceStateUpdate(BrowserApplication Application, BrowserTabWorkspace Workspace);

    private sealed record SessionRestoreUpdate(BrowserApplication Application, BrowserSessionRestoreResult Result);

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
