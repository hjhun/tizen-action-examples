using Browser.App;
using Browser.Domain;
using Browser.UseCases;

var matrix = new[]
{
    new ViewportCase(1920, 1080, 0, 0, 0, 0, 1.0f, 0, 0),
    new ViewportCase(1280, 720, 0, 0, 0, 0, 2.0f / 3.0f, 0, 0),
    new ViewportCase(1440, 1080, 0, 0, 0, 0, 0.75f, 0, 135),
    new ViewportCase(2560, 1080, 0, 0, 0, 0, 1.0f, 320, 0),
    new ViewportCase(1920, 1080, 30, 20, 10, 40, 0.9444444f, 63.3333f, 20),
};

foreach (var item in matrix)
{
    if (!ReferenceCanvasViewport.TryCreate(
            item.WindowWidth,
            item.WindowHeight,
            item.InsetStart,
            item.InsetTop,
            item.InsetEnd,
            item.InsetBottom,
            out var viewport) ||
        !Near(viewport.Scale, item.Scale) ||
        !Near(viewport.OffsetX, item.OffsetX) ||
        !Near(viewport.OffsetY, item.OffsetY))
    {
        throw new InvalidOperationException($"Unexpected viewport for {item}: {viewport}");
    }
}

foreach (var invalid in new[]
         {
             new[] { 0f, 1080f, 0f, 0f, 0f, 0f },
             new[] { 1920f, -1f, 0f, 0f, 0f, 0f },
             new[] { 100f, 100f, 50f, 0f, 50f, 0f },
             new[] { float.NaN, 1080f, 0f, 0f, 0f, 0f },
         })
{
    if (ReferenceCanvasViewport.TryCreate(invalid[0], invalid[1], invalid[2], invalid[3], invalid[4], invalid[5], out _))
    {
        throw new InvalidOperationException("Invalid drawable geometry must retain the prior native frame.");
    }
}

if (BrowserShellMetrics.HeaderHeight != 84 ||
    BrowserShellMetrics.ContextHeight != 0 ||
    BrowserShellMetrics.ProgressHeight != 6 ||
    BrowserShellMetrics.ContentLeft != 40 ||
    BrowserShellMetrics.ContentTop != 90 ||
    BrowserShellMetrics.ContentWidth != 1840 ||
    BrowserShellMetrics.ContentHeight != 970 ||
    BrowserShellMetrics.ContentTop + BrowserShellMetrics.ContentHeight != 1060 ||
    BrowserShellMetrics.DockLeft != 740 ||
    BrowserShellMetrics.DockTop != 988 ||
    BrowserShellMetrics.DockWidth != 440 ||
    BrowserShellMetrics.DockHeight != 64 ||
    BrowserShellMetrics.DockTop + BrowserShellMetrics.DockHeight > BrowserShellMetrics.DesignHeight)
{
    throw new InvalidOperationException("NUI split address/content/navigation geometry must match the executable 1920x1080 Browser contract.");
}

if (BrowserAddressMetrics.ShellLeft != 266 ||
    BrowserAddressMetrics.ShellTop != 12 ||
    BrowserAddressMetrics.ShellWidth != 1540 ||
    BrowserAddressMetrics.ShellHeight != 58 ||
    BrowserAddressMetrics.TextInsetX != 18 ||
    BrowserAddressMetrics.TextTopOffset != 12 ||
    BrowserAddressMetrics.TextWidth != 1504 ||
    BrowserAddressMetrics.TextHeight != 34 ||
    BrowserAddressMetrics.FocusOutlineWidth != 3)
{
    throw new InvalidOperationException("Native address text must use the approved inset shell geometry instead of the platform field baseline.");
}

if (!BrowserTabFocusPolicy.ShouldFocusAddress(keepTabsOpen: false, isSessionRestore: false) ||
    BrowserTabFocusPolicy.ShouldFocusAddress(keepTabsOpen: true, isSessionRestore: false) ||
    BrowserTabFocusPolicy.ShouldFocusAddress(keepTabsOpen: false, isSessionRestore: true) ||
    !BrowserTabFocusPolicy.ShouldRestoreWorkspaceFocus(isInitialRender: false, isSessionRestore: false) ||
    BrowserTabFocusPolicy.ShouldRestoreWorkspaceFocus(isInitialRender: true, isSessionRestore: false) ||
    BrowserTabFocusPolicy.ShouldRestoreWorkspaceFocus(isInitialRender: false, isSessionRestore: true))
{
    throw new InvalidOperationException("Initial/session hydration must avoid address focus and deterministically focus a restored WebView only after Page navigation completes.");
}

var restoredFocus = new BrowserRestoredFocusTracker();
restoredFocus.BeginRestore();
if (restoredFocus.Observe(new BrowserNavigationState(10, BrowserNavigationPhase.Loading, null, "https://example.com/", null, default)) ||
    !restoredFocus.Observe(new BrowserNavigationState(10, BrowserNavigationPhase.Page, BrowserPage.Create("page-1", "https://example.com/", "Example", string.Empty), "https://example.com/", null, default)))
{
    throw new InvalidOperationException("A restored Page must request WebView focus only for its captured terminal navigation intent.");
}

// A paused terminal Page remains consumable on resume until focus succeeds.
if (!restoredFocus.Observe(new BrowserNavigationState(10, BrowserNavigationPhase.Page, BrowserPage.Create("page-1", "https://example.com/", "Example", string.Empty), "https://example.com/", null, default)))
{
    throw new InvalidOperationException("Deferred restored focus must survive pause/resume until explicitly completed.");
}

restoredFocus.CompleteFocus();
if (restoredFocus.Observe(new BrowserNavigationState(10, BrowserNavigationPhase.Page, BrowserPage.Create("page-1", "https://example.com/", "Example", string.Empty), "https://example.com/", null, default)))
{
    throw new InvalidOperationException("Completed restored focus must be consumed exactly once.");
}

restoredFocus.BeginRestore();
restoredFocus.Observe(new BrowserNavigationState(20, BrowserNavigationPhase.Loading, null, "https://example.com/", null, default));
restoredFocus.Observe(new BrowserNavigationState(21, BrowserNavigationPhase.Loading, null, "https://other.example/", null, default));
if (restoredFocus.Observe(new BrowserNavigationState(21, BrowserNavigationPhase.Page, BrowserPage.Create("page-1", "https://other.example/", "Other", string.Empty), "https://other.example/", null, default)))
{
    throw new InvalidOperationException("A superseding navigation must invalidate restored WebView focus intent.");
}

if (BrowserInitialFocusPolicy.Resolve(showsHomeSurface: true) != BrowserInitialFocusTarget.Reload ||
    BrowserInitialFocusPolicy.Resolve(showsHomeSurface: false) != BrowserInitialFocusTarget.Reload ||
    !BrowserHiddenHomeFocusPolicy.ShouldFocusWebView(isHomeControlFocused: true, BrowserNavigationPhase.Page) ||
    BrowserHiddenHomeFocusPolicy.ShouldFocusWebView(isHomeControlFocused: true, BrowserNavigationPhase.Loading) ||
    BrowserHiddenHomeFocusPolicy.ShouldFocusWebView(isHomeControlFocused: false, BrowserNavigationPhase.Page))
{
    throw new InvalidOperationException("Explicit Home focus must stay visible, while a hidden Home control must yield to the terminal WebView.");
}

if (!BrowserAddressInteractionPolicy.ShouldRequestEditing(pressStarted: true, modal: false) ||
    BrowserAddressInteractionPolicy.ShouldRequestEditing(pressStarted: false, modal: false) ||
    BrowserAddressInteractionPolicy.ShouldRequestEditing(pressStarted: true, modal: true))
{
    throw new InvalidOperationException("The visual address shell must forward a real press to TextField editing without crossing a modal boundary.");
}

if (BrowserTypographyMetrics.ProductPointSize != 4.0f ||
    BrowserTypographyMetrics.AddressPointSize != 4.0f ||
    BrowserTypographyMetrics.HomeTitlePointSize != 8.5f ||
    BrowserTypographyMetrics.BodyPointSize != 4.3f ||
    BrowserTypographyMetrics.TabsTitlePointSize != 8.5f ||
    BrowserTypographyMetrics.TabTitlePointSize != 4.7f ||
    BrowserTypographyMetrics.TabMetaPointSize != 3.3f ||
    BrowserTypographyMetrics.DialogTitlePointSize != 6.3f ||
    BrowserTabsMetrics.ColumnCount != 2 ||
    BrowserTabsMetrics.CardWidth != 730 ||
    BrowserTabsMetrics.CardHeight != 214)
{
    throw new InvalidOperationException("Native typography and tab-card metrics must match the approved HTML contract before screenshot calibration.");
}

var unavailableHistory = BrowserShellFocusGraph.Create(backEnabled: false, forwardEnabled: false);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, -1), BrowserShellFocusTarget.Address);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, 1), BrowserShellFocusTarget.Reload);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Address);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Tabs, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveDown(BrowserShellFocusTarget.Address), BrowserShellFocusTarget.WebContent);
AssertFocus(unavailableHistory.MoveDown(BrowserShellFocusTarget.WebContent), BrowserShellFocusTarget.Home);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Home, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveUp(BrowserShellFocusTarget.Tabs), BrowserShellFocusTarget.WebContent);
AssertFocus(unavailableHistory.MoveUp(BrowserShellFocusTarget.WebContent), BrowserShellFocusTarget.Address);

var backOnly = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: false);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Back, 1), BrowserShellFocusTarget.Home);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Home, -1), BrowserShellFocusTarget.Back);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Home, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Tabs, -1), BrowserShellFocusTarget.Home);

var fullHistory = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: true);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Forward, -1), BrowserShellFocusTarget.Back);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Forward, 1), BrowserShellFocusTarget.Home);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Home, 1), BrowserShellFocusTarget.Tabs);

var loading = BrowserShellFocusGraph.Create(backEnabled: false, forwardEnabled: false, reloadEnabled: false);
AssertFocus(loading.MoveHorizontal(BrowserShellFocusTarget.Address, 1), BrowserShellFocusTarget.Address);
AssertFocus(loading.MoveHorizontal(BrowserShellFocusTarget.Tabs, -1), BrowserShellFocusTarget.Home);

var loadingVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    1, BrowserNavigationPhase.Loading, null, "https://example.com/", null, default));
var offlineVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    2, BrowserNavigationPhase.Offline, null, "https://example.com/", "offline", default));
var homeVisual = BrowserNavigationVisualState.From(BrowserNavigationState.Initial);
var invalidVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    3, BrowserNavigationPhase.InvalidInput, null, null, "invalid", default));
if (!loadingVisual.ShowsProgress || loadingVisual.ShowsRecovery || loadingVisual.ReloadEnabled ||
    offlineVisual.ShowsProgress || !offlineVisual.ShowsRecovery || offlineVisual.Title != "You're offline" ||
    homeVisual.Title != "Start page" || homeVisual.Status != "HOME" || !homeVisual.ReloadEnabled ||
    invalidVisual.Title != "Check the address" || invalidVisual.Status != "CHECK")
{
    throw new InvalidOperationException("Home, loading, and recovery phases must use unclipped deterministic NUI labels.");
}

if (BrowserRecoveryFocusGraph.Move(BrowserRecoveryFocusTarget.Retry, -1) != BrowserRecoveryFocusTarget.Retry ||
    BrowserRecoveryFocusGraph.Move(BrowserRecoveryFocusTarget.Retry, 1) != BrowserRecoveryFocusTarget.Back ||
    BrowserRecoveryFocusGraph.Move(BrowserRecoveryFocusTarget.Back, 1) != BrowserRecoveryFocusTarget.EditAddress ||
    BrowserRecoveryFocusGraph.Move(BrowserRecoveryFocusTarget.EditAddress, 1) != BrowserRecoveryFocusTarget.EditAddress)
{
    throw new InvalidOperationException("Recovery focus must remain trapped in Retry, Back, Edit address order.");
}

if (BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.TizenDocs, -1) != BrowserHomeFocusTarget.TizenDocs ||
    BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.TizenDocs, 1) != BrowserHomeFocusTarget.TizenOrg ||
    BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.TizenOrg, 1) != BrowserHomeFocusTarget.NewTab ||
    BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.NewTab, 1) != BrowserHomeFocusTarget.NewTab)
{
    throw new InvalidOperationException("Home focus must remain trapped in Tizen Docs, Tizen.org, New tab order.");
}

var blankTitleTab = BrowserTab.Create(
    "blank-title",
    BrowserPage.Create("blank-title", "https://example.com/blank", string.Empty, "Public"));
var longTitleTab = BrowserTab.Create(
    "long-title",
    BrowserPage.Create("long-title", "https://example.com/long", new string('T', 90), "Public"));
if (BrowserTabVisualText.Title(blankTitleTab) != "New tab" || BrowserTabVisualText.Title(longTitleTab).Length != 80)
{
    throw new InvalidOperationException("Tab visuals must safely fall back for blank target titles and bound long labels.");
}

var homeWorkspace = BrowserTabWorkspace.Create("tab-1");
var homeWorkspaceVisual = BrowserWorkspaceVisualState.From(homeWorkspace);
homeWorkspace = homeWorkspace.OpenTabs();
homeWorkspace.TryCreateTab(out var twoTabs, out var secondWorkspaceTab);
twoTabs.TryRequestClose(secondWorkspaceTab, out var closeWorkspaceVisualSource);
var tabsVisual = BrowserWorkspaceVisualState.From(twoTabs);
var modalVisual = BrowserWorkspaceVisualState.From(closeWorkspaceVisualSource);
var maxTabsWorkspace = BrowserTabWorkspace.Create("tab-1").OpenTabs();
for (var index = 1; index < BrowserTabWorkspace.MaximumTabs; index++)
{
    maxTabsWorkspace.TryCreateTab(out maxTabsWorkspace, out _);
}
var maxTabsVisual = BrowserWorkspaceVisualState.From(maxTabsWorkspace);
if (!homeWorkspaceVisual.ShowsHome || homeWorkspaceVisual.ShowsTabs ||
    !tabsVisual.ShowsTabs || tabsVisual.ShowsCloseConfirmation ||
    !modalVisual.ShowsTabs || !modalVisual.ShowsCloseConfirmation ||
    modalVisual.PreferredFocus != BrowserWorkspaceFocus.CancelClose || maxTabsVisual.NewTabEnabled)
{
    throw new InvalidOperationException("Home, Tabs, and close confirmation must map to exclusive deterministic NUI overlays and focus.");
}

var actionPage = BrowserPage.Create("agent-page", "https://example.com/action", "Action", "Public");
var actionWorkspace = BrowserTabWorkspace.Create("selected-tab");
if (!BrowserActionNavigationTargetContract.TryCreate(
        actionWorkspace, true, actionPage, out var actionTarget) ||
    actionTarget.SelectedTabId != "selected-tab" || actionTarget.Url != actionPage.Url ||
    BrowserActionNavigationTargetContract.TryCreate(homeWorkspace.OpenTabs(), true, actionPage, out _) ||
    BrowserActionNavigationTargetContract.TryCreate(actionWorkspace, false, actionPage, out _))
{
    throw new InvalidOperationException("Action Go must target the visible selected tab ID, not the caller-supplied Entity ID.");
}

Console.WriteLine("PASS: Browser NUI shell geometry, safe viewport, navigation/recovery visuals, and deterministic focus contracts.");

static bool Near(float actual, float expected) => MathF.Abs(actual - expected) < 0.002f;

static void AssertFocus(BrowserShellFocusTarget actual, BrowserShellFocusTarget expected)
{
    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected focus {expected}, got {actual}.");
    }
}

internal sealed record ViewportCase(
    float WindowWidth,
    float WindowHeight,
    float InsetStart,
    float InsetTop,
    float InsetEnd,
    float InsetBottom,
    float Scale,
    float OffsetX,
    float OffsetY);
