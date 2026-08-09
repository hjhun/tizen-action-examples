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
    public const float HeaderHeight = 118.0f;
    public const float ContextHeight = 0.0f;
    public const float ProgressHeight = 6.0f;
    public const float ContentLeft = 40.0f;
    public const float ContentTop = HeaderHeight + ContextHeight + ProgressHeight;
    public const float ContentWidth = 1840.0f;
    public const float ContentHeight = 924.0f;
    public const float DockLeft = 590.0f;
    public const float DockTop = 960.0f;
    public const float DockWidth = 740.0f;
    public const float DockHeight = 92.0f;
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
        var dockRow = new List<BrowserShellFocusTarget>(3);
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

        return current == BrowserShellFocusTarget.WebContent ? BrowserShellFocusTarget.Tabs : current;
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
            state.Phase != BrowserNavigationPhase.Loading && state.Page is not null);
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
    OpenGuide,
    EditAddress,
}

public static class BrowserHomeFocusGraph
{
    private static readonly BrowserHomeFocusTarget[] Row =
        [BrowserHomeFocusTarget.OpenGuide, BrowserHomeFocusTarget.EditAddress];

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
