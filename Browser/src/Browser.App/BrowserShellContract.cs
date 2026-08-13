using Browser.Domain;
using Browser.UseCases;

namespace Browser.App;

/// <summary>
/// The single 1920x1080 reference-canvas contract shared by the NUI shell and host tests.
/// </summary>
public static class BrowserShellMetrics
{
    public const float DesignWidth = 1920.0f;
    public const float DesignHeight = 1080.0f;
    public const float HeaderHeight = 84.0f;
    public const float ContextHeight = 0.0f;
    public const float ProgressHeight = 6.0f;
    public const float ContentLeft = 40.0f;
    public const float ContentTop = HeaderHeight + ContextHeight + ProgressHeight;
    public const float ContentWidth = 1840.0f;
    public const float ContentHeight = 970.0f;
    public const float DockLeft = 740.0f;
    public const float DockTop = 988.0f;
    public const float DockWidth = 440.0f;
    public const float DockHeight = 64.0f;
}

public static class BrowserTypographyMetrics
{
    public const float ProductPointSize = 4.0f;
    public const float AddressPointSize = 4.0f;
    public const float HomeTitlePointSize = 8.5f;
    public const float BodyPointSize = 4.3f;
    public const float TabsTitlePointSize = 8.5f;
    public const float TabTitlePointSize = 4.7f;
    public const float TabMetaPointSize = 3.3f;
    public const float DialogTitlePointSize = 6.3f;
    public const float ActionPointSize = 3.7f;
}

/// <summary>
/// Approved address capsule geometry. The native TextField is inset inside a separate visual
/// shell so its platform baseline cannot pull the URL against the capsule edge.
/// </summary>
public static class BrowserAddressMetrics
{
    public const float ShellLeft = 266.0f;
    public const float ShellTop = 12.0f;
    public const float ShellWidth = 1540.0f;
    public const float ShellHeight = 58.0f;
    public const float TextInsetX = 18.0f;
    public const float TextTopOffset = 12.0f;
    public const float TextWidth = ShellWidth - (TextInsetX * 2.0f);
    public const float TextHeight = 34.0f;
    public const float FocusOutlineWidth = 3.0f;
}

public static class BrowserAddressInteractionPolicy
{
    public static bool ShouldRequestEditing(bool pressStarted, bool modal) => pressStarted && !modal;
}

/// <summary>
/// Session hydration is passive and must not raise the IME. Explicit tab activation remains an
/// address-editing action unless the tabs surface is intentionally kept open.
/// </summary>
public static class BrowserTabFocusPolicy
{
    public static bool ShouldFocusAddress(bool keepTabsOpen, bool isSessionRestore) =>
        !keepTabsOpen && !isSessionRestore;

    public static bool ShouldRestoreWorkspaceFocus(bool isInitialRender, bool isSessionRestore) =>
        !isInitialRender && !isSessionRestore;

}

/// <summary>
/// Correlates restored-page focus with the navigation intent that hydration started.
/// The terminal Page remains pending until focus succeeds, which makes pause/resume safe.
/// </summary>
public sealed class BrowserRestoredFocusTracker
{
    private bool _captureNextLoading;
    private long? _intentId;

    public void BeginRestore()
    {
        _captureNextLoading = true;
        _intentId = null;
    }

    public bool Observe(BrowserNavigationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_captureNextLoading)
        {
            if (state.Phase == BrowserNavigationPhase.Loading)
            {
                _intentId = state.IntentId;
                _captureNextLoading = false;
            }

            return false;
        }

        if (_intentId is not { } intentId)
        {
            return false;
        }

        if (state.IntentId > intentId)
        {
            _intentId = null;
            return false;
        }

        if (state.IntentId < intentId)
        {
            return false;
        }

        if (state.Phase == BrowserNavigationPhase.Page && state.Page is not null)
        {
            return true;
        }

        if (state.Phase != BrowserNavigationPhase.Loading)
        {
            _intentId = null;
        }

        return false;
    }

    public void CompleteFocus()
    {
        _captureNextLoading = false;
        _intentId = null;
    }
}

public enum BrowserInitialFocusTarget
{
    Reload,
    HomeQuickAccess,
    HomeDock,
}

public static class BrowserInitialFocusPolicy
{
    public static BrowserInitialFocusTarget Resolve(bool showsHomeSurface) =>
        BrowserInitialFocusTarget.Reload;
}

public static class BrowserHiddenHomeFocusPolicy
{
    public static bool ShouldFocusWebView(
        bool isHomeControlFocused,
        BrowserNavigationPhase phase) =>
        isHomeControlFocused && phase == BrowserNavigationPhase.Page;
}

public static class BrowserTabsMetrics
{
    public const int ColumnCount = 2;
    public const float GridLeft = 210.0f;
    public const float GridTop = 146.0f;
    public const float CardWidth = 730.0f;
    public const float CardHeight = 214.0f;
    public const float ColumnGap = 20.0f;
    public const float RowGap = 20.0f;
}

/// <summary>
/// A validated centered-uniform transform for the drawable window area. Invalid transient
/// measurements are rejected so callers can retain their last known-good native frame.
/// </summary>
public readonly record struct ReferenceCanvasViewport(float Scale, float OffsetX, float OffsetY)
{
    public static bool TryCreate(
        float windowWidth,
        float windowHeight,
        float insetStart,
        float insetTop,
        float insetEnd,
        float insetBottom,
        out ReferenceCanvasViewport viewport)
    {
        viewport = default;
        if (!IsPositiveFinite(windowWidth) || !IsPositiveFinite(windowHeight) ||
            !IsNonNegativeFinite(insetStart) || !IsNonNegativeFinite(insetTop) ||
            !IsNonNegativeFinite(insetEnd) || !IsNonNegativeFinite(insetBottom))
        {
            return false;
        }

        var availableWidth = windowWidth - insetStart - insetEnd;
        var availableHeight = windowHeight - insetTop - insetBottom;
        if (!IsPositiveFinite(availableWidth) || !IsPositiveFinite(availableHeight))
        {
            return false;
        }

        var scale = MathF.Min(
            availableWidth / BrowserShellMetrics.DesignWidth,
            availableHeight / BrowserShellMetrics.DesignHeight);
        if (!IsPositiveFinite(scale))
        {
            return false;
        }

        var offsetX = insetStart + ((availableWidth - (BrowserShellMetrics.DesignWidth * scale)) / 2.0f);
        var offsetY = insetTop + ((availableHeight - (BrowserShellMetrics.DesignHeight * scale)) / 2.0f);
        if (!float.IsFinite(offsetX) || !float.IsFinite(offsetY))
        {
            return false;
        }

        viewport = new ReferenceCanvasViewport(scale, offsetX, offsetY);
        return true;
    }

    private static bool IsPositiveFinite(float value) => float.IsFinite(value) && value > 0.0f;

    private static bool IsNonNegativeFinite(float value) => float.IsFinite(value) && value >= 0.0f;
}

public enum BrowserShellFocusTarget
{
    Back,
    Forward,
    Home,
    Reload,
    Address,
    Tabs,
    WebContent,
}

/// <summary>
/// Deterministic split-row focus policy. Disabled history controls are omitted rather than
/// accepting focus, while vertical movement crosses top address, content, and bottom dock.
/// </summary>
public sealed class BrowserShellFocusGraph
{
    private readonly BrowserShellFocusTarget[] _topRow;
    private readonly BrowserShellFocusTarget[] _dockRow;

    private BrowserShellFocusGraph(BrowserShellFocusTarget[] topRow, BrowserShellFocusTarget[] dockRow)
    {
        _topRow = topRow;
        _dockRow = dockRow;
    }

    public static BrowserShellFocusGraph Create(bool backEnabled, bool forwardEnabled, bool reloadEnabled = true)
    {
        var dockRow = new List<BrowserShellFocusTarget>(4);
        if (backEnabled)
        {
            dockRow.Add(BrowserShellFocusTarget.Back);
        }

        if (forwardEnabled)
        {
            dockRow.Add(BrowserShellFocusTarget.Forward);
        }

        var topRow = new List<BrowserShellFocusTarget>(2) { BrowserShellFocusTarget.Address };
        if (reloadEnabled)
        {
            topRow.Add(BrowserShellFocusTarget.Reload);
        }

        dockRow.Add(BrowserShellFocusTarget.Home);
        dockRow.Add(BrowserShellFocusTarget.Tabs);
        return new BrowserShellFocusGraph(topRow.ToArray(), dockRow.ToArray());
    }

    public BrowserShellFocusTarget MoveHorizontal(BrowserShellFocusTarget current, int delta)
    {
        var row = Array.IndexOf(_topRow, current) >= 0 ? _topRow : _dockRow;
        var index = Array.IndexOf(row, current);
        if (index < 0)
        {
            return BrowserShellFocusTarget.Address;
        }

        return row[Math.Clamp(index + Math.Sign(delta), 0, row.Length - 1)];
    }

    public BrowserShellFocusTarget MoveDown(BrowserShellFocusTarget current)
    {
        if (Array.IndexOf(_topRow, current) >= 0)
        {
            return BrowserShellFocusTarget.WebContent;
        }

        return current == BrowserShellFocusTarget.WebContent ? BrowserShellFocusTarget.Home : current;
    }

    public BrowserShellFocusTarget MoveUp(BrowserShellFocusTarget current)
    {
        if (Array.IndexOf(_dockRow, current) >= 0)
        {
            return BrowserShellFocusTarget.WebContent;
        }

        return current == BrowserShellFocusTarget.WebContent ? BrowserShellFocusTarget.Address : current;
    }
}

public sealed record BrowserNavigationVisualState(
    string Title,
    string Status,
    bool ShowsProgress,
    bool ShowsRecovery,
    bool ReloadEnabled)
{
    public static BrowserNavigationVisualState From(BrowserNavigationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var recovery = state.Phase is BrowserNavigationPhase.Offline or BrowserNavigationPhase.EngineError or
            BrowserNavigationPhase.Timeout or BrowserNavigationPhase.InvalidInput;
        return new BrowserNavigationVisualState(
            state.Phase switch
            {
                BrowserNavigationPhase.Home => "Start page",
                BrowserNavigationPhase.Loading => "Loading page",
                BrowserNavigationPhase.Page => state.Page?.Title ?? "Web page",
                BrowserNavigationPhase.Offline => "You're offline",
                BrowserNavigationPhase.EngineError => "Browser engine unavailable",
                BrowserNavigationPhase.Timeout => "This page took too long",
                BrowserNavigationPhase.InvalidInput => "Check the address",
                _ => "Page unavailable",
            },
            state.Phase switch
            {
                BrowserNavigationPhase.Home => "HOME",
                BrowserNavigationPhase.Loading => "LOADING",
                BrowserNavigationPhase.Page => "READY",
                BrowserNavigationPhase.Offline => "OFFLINE",
                BrowserNavigationPhase.EngineError => "ERROR",
                BrowserNavigationPhase.Timeout => "TIMEOUT",
                BrowserNavigationPhase.InvalidInput => "CHECK",
                _ => "ERROR",
            },
            state.Phase == BrowserNavigationPhase.Loading,
            recovery,
            state.Phase is BrowserNavigationPhase.Home or BrowserNavigationPhase.Page);
    }
}

public enum BrowserRecoveryFocusTarget
{
    Retry,
    Back,
    EditAddress,
}

public static class BrowserRecoveryFocusGraph
{
    private static readonly BrowserRecoveryFocusTarget[] Row =
        [BrowserRecoveryFocusTarget.Retry, BrowserRecoveryFocusTarget.Back, BrowserRecoveryFocusTarget.EditAddress];

    public static BrowserRecoveryFocusTarget Move(BrowserRecoveryFocusTarget current, int delta)
    {
        var index = Array.IndexOf(Row, current);
        return Row[Math.Clamp(index + Math.Sign(delta), 0, Row.Length - 1)];
    }
}

public enum BrowserHomeFocusTarget
{
    TizenDocs,
    TizenOrg,
    NewTab,
}

public static class BrowserHomeFocusGraph
{
    private static readonly BrowserHomeFocusTarget[] Row =
        [BrowserHomeFocusTarget.TizenDocs, BrowserHomeFocusTarget.TizenOrg, BrowserHomeFocusTarget.NewTab];

    public static BrowserHomeFocusTarget Move(BrowserHomeFocusTarget current, int delta)
    {
        var index = Array.IndexOf(Row, current);
        return Row[Math.Clamp(index + Math.Sign(delta), 0, Row.Length - 1)];
    }
}

public static class BrowserTabVisualText
{
    public static string Title(BrowserTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var title = string.IsNullOrWhiteSpace(tab.Page?.Title) ? "New tab" : tab.Page.Title;
        return title.Length <= 80 ? title : title[..80];
    }
}

public sealed record BrowserWorkspaceVisualState(
    bool ShowsHome,
    bool ShowsTabs,
    bool ShowsCloseConfirmation,
    bool NewTabEnabled,
    BrowserWorkspaceFocus PreferredFocus,
    string? PreferredFocusTabId)
{
    public static BrowserWorkspaceVisualState From(BrowserTabWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var tabsVisible = workspace.Surface is BrowserWorkspaceSurface.Tabs or BrowserWorkspaceSurface.CloseConfirmation;
        return new BrowserWorkspaceVisualState(
            !tabsVisible && workspace.SelectedTab.Page is null,
            tabsVisible,
            workspace.Surface == BrowserWorkspaceSurface.CloseConfirmation,
            workspace.CanCreateTab,
            workspace.PreferredFocus,
            workspace.PreferredFocusTabId);
    }
}
