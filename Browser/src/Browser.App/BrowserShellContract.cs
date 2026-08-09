namespace Browser.App;

/// <summary>
/// The single 1920x1080 reference-canvas contract shared by the NUI shell and host tests.
/// </summary>
public static class BrowserShellMetrics
{
    public const float DesignWidth = 1920.0f;
    public const float DesignHeight = 1080.0f;
    public const float HeaderHeight = 132.0f;
    public const float ContextHeight = 92.0f;
    public const float ProgressHeight = 6.0f;
    public const float ContentLeft = 52.0f;
    public const float ContentTop = HeaderHeight + ContextHeight + ProgressHeight;
    public const float ContentWidth = 1816.0f;
    public const float ContentHeight = 806.0f;
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
/// Deterministic command-row focus policy. Disabled history controls are omitted rather than
/// accepting focus, while vertical movement has one stable WebView/address restoration pair.
/// </summary>
public sealed class BrowserShellFocusGraph
{
    private readonly BrowserShellFocusTarget[] _commandRow;

    private BrowserShellFocusGraph(BrowserShellFocusTarget[] commandRow) => _commandRow = commandRow;

    public static BrowserShellFocusGraph Create(bool backEnabled, bool forwardEnabled)
    {
        var commandRow = new List<BrowserShellFocusTarget>(5);
        if (backEnabled)
        {
            commandRow.Add(BrowserShellFocusTarget.Back);
        }

        if (forwardEnabled)
        {
            commandRow.Add(BrowserShellFocusTarget.Forward);
        }

        commandRow.Add(BrowserShellFocusTarget.Reload);
        commandRow.Add(BrowserShellFocusTarget.Address);
        commandRow.Add(BrowserShellFocusTarget.Tabs);
        return new BrowserShellFocusGraph(commandRow.ToArray());
    }

    public BrowserShellFocusTarget MoveHorizontal(BrowserShellFocusTarget current, int delta)
    {
        var index = Array.IndexOf(_commandRow, current);
        if (index < 0)
        {
            return BrowserShellFocusTarget.Address;
        }

        return _commandRow[Math.Clamp(index + Math.Sign(delta), 0, _commandRow.Length - 1)];
    }

    public BrowserShellFocusTarget MoveDown(BrowserShellFocusTarget current) =>
        current == BrowserShellFocusTarget.WebContent ? current : BrowserShellFocusTarget.WebContent;

    public BrowserShellFocusTarget MoveUp(BrowserShellFocusTarget current) =>
        current == BrowserShellFocusTarget.WebContent ? BrowserShellFocusTarget.Address : current;
}
