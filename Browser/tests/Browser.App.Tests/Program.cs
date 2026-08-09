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

if (BrowserShellMetrics.HeaderHeight != 132 ||
    BrowserShellMetrics.ContextHeight != 92 ||
    BrowserShellMetrics.ProgressHeight != 6 ||
    BrowserShellMetrics.ContentLeft != 52 ||
    BrowserShellMetrics.ContentTop != 230 ||
    BrowserShellMetrics.ContentWidth != 1816 ||
    BrowserShellMetrics.ContentHeight != 806 ||
    BrowserShellMetrics.ContentTop + BrowserShellMetrics.ContentHeight != 1036)
{
    throw new InvalidOperationException("NUI shell geometry must match the executable 1920x1080 Browser contract.");
}

var unavailableHistory = BrowserShellFocusGraph.Create(backEnabled: false, forwardEnabled: false);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, -1), BrowserShellFocusTarget.Reload);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Reload);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Tabs, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveDown(BrowserShellFocusTarget.Address), BrowserShellFocusTarget.WebContent);
AssertFocus(unavailableHistory.MoveUp(BrowserShellFocusTarget.WebContent), BrowserShellFocusTarget.Address);

var backOnly = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: false);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Back);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Back, 1), BrowserShellFocusTarget.Reload);

var fullHistory = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: true);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Forward);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Forward, -1), BrowserShellFocusTarget.Back);

var loading = BrowserShellFocusGraph.Create(backEnabled: false, forwardEnabled: false, reloadEnabled: false);
AssertFocus(loading.MoveHorizontal(BrowserShellFocusTarget.Address, -1), BrowserShellFocusTarget.Address);
AssertFocus(loading.MoveHorizontal(BrowserShellFocusTarget.Address, 1), BrowserShellFocusTarget.Tabs);

var loadingVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    1, BrowserNavigationPhase.Loading, null, "https://example.com/", null, default));
var offlineVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    2, BrowserNavigationPhase.Offline, null, "https://example.com/", "offline", default));
var homeVisual = BrowserNavigationVisualState.From(BrowserNavigationState.Initial);
var invalidVisual = BrowserNavigationVisualState.From(new BrowserNavigationState(
    3, BrowserNavigationPhase.InvalidInput, null, null, "invalid", default));
if (!loadingVisual.ShowsProgress || loadingVisual.ShowsRecovery || loadingVisual.ReloadEnabled ||
    offlineVisual.ShowsProgress || !offlineVisual.ShowsRecovery || offlineVisual.Title != "You're offline" ||
    homeVisual.Title != "Start page" || homeVisual.Status != "HOME" ||
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

if (BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.OpenGuide, -1) != BrowserHomeFocusTarget.OpenGuide ||
    BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.OpenGuide, 1) != BrowserHomeFocusTarget.EditAddress ||
    BrowserHomeFocusGraph.Move(BrowserHomeFocusTarget.EditAddress, 1) != BrowserHomeFocusTarget.EditAddress)
{
    throw new InvalidOperationException("Home focus must remain trapped in Open guide, Enter address order.");
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
