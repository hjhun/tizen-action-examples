using Browser.Domain;

namespace Browser.UseCases;

public enum BrowserAgentSurface
{
    Hidden,
    Home,
    Loading,
    Page,
    Offline,
    EngineError,
    Timeout,
    InvalidInput,
    Tabs,
    CloseConfirmation,
}

public sealed record BrowserAgentSnapshot(long Revision, BrowserAgentSurface Surface, BrowserPage? Page);

/// <summary>
/// Publishes the single privacy-safe Browser state observed by Action, View, and Presentation paths.
/// Transient, secondary, and lifecycle-hidden surfaces never retain a prior page Entity.
/// </summary>
public sealed class BrowserAgentStateRegistry
{
    private BrowserAgentSnapshot _current = new(0, BrowserAgentSurface.Hidden, null);
    private long _revision;

    public BrowserAgentSnapshot Current => Volatile.Read(ref _current);

    public void Publish(
        BrowserNavigationState navigation,
        BrowserWorkspaceSurface workspace,
        string? selectedTabId,
        bool isApplicationVisible)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var (surface, page) = Project(navigation, workspace, selectedTabId, isApplicationVisible);
        Volatile.Write(
            ref _current,
            new BrowserAgentSnapshot(Interlocked.Increment(ref _revision), surface, page));
    }

    private static (BrowserAgentSurface Surface, BrowserPage? Page) Project(
        BrowserNavigationState navigation,
        BrowserWorkspaceSurface workspace,
        string? selectedTabId,
        bool isApplicationVisible)
    {
        if (!isApplicationVisible)
        {
            return (BrowserAgentSurface.Hidden, null);
        }

        if (workspace == BrowserWorkspaceSurface.Tabs)
        {
            return (BrowserAgentSurface.Tabs, null);
        }

        if (workspace == BrowserWorkspaceSurface.CloseConfirmation)
        {
            return (BrowserAgentSurface.CloseConfirmation, null);
        }

        return navigation.Phase switch
        {
            BrowserNavigationPhase.Home => (BrowserAgentSurface.Home, null),
            BrowserNavigationPhase.Loading => (BrowserAgentSurface.Loading, null),
            BrowserNavigationPhase.Page when navigation.Page is not null &&
                string.Equals(navigation.Page.Id, selectedTabId, StringComparison.Ordinal) =>
                (BrowserAgentSurface.Page, navigation.Page),
            BrowserNavigationPhase.Offline => (BrowserAgentSurface.Offline, null),
            BrowserNavigationPhase.EngineError => (BrowserAgentSurface.EngineError, null),
            BrowserNavigationPhase.Timeout => (BrowserAgentSurface.Timeout, null),
            BrowserNavigationPhase.InvalidInput => (BrowserAgentSurface.InvalidInput, null),
            _ => (BrowserAgentSurface.Hidden, null),
        };
    }
}

public readonly record struct BrowserWindowBounds(double X, double Y, double Width, double Height);

public sealed record BrowserPageViewSnapshot
{
    private BrowserPageViewSnapshot(
        BrowserPage page,
        double screenX,
        double screenY,
        double? windowX,
        double? windowY,
        double width,
        double height,
        bool isFocused)
    {
        Page = page;
        ScreenX = screenX;
        ScreenY = screenY;
        WindowX = windowX;
        WindowY = windowY;
        Width = width;
        Height = height;
        IsFocused = isFocused;
    }

    public BrowserPage Page { get; }
    public string ViewId => $"browser:page:{Page.Id}";
    public double ScreenX { get; }
    public double ScreenY { get; }
    public double? WindowX { get; }
    public double? WindowY { get; }
    public double Width { get; }
    public double Height { get; }
    public bool IsFocused { get; }
    public BrowserWindowBounds WireWindowBounds =>
        WindowX is { } x && WindowY is { } y
            ? new BrowserWindowBounds(x, y, Width, Height)
            : default;

    public static bool TryCreate(
        BrowserPage page,
        double screenX,
        double screenY,
        double? windowX,
        double? windowY,
        double width,
        double height,
        bool isFocused,
        out BrowserPageViewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(page);
        snapshot = null!;
        if (!double.IsFinite(screenX) || !double.IsFinite(screenY) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 ||
            (windowX.HasValue != windowY.HasValue) ||
            (windowX is { } x && !double.IsFinite(x)) ||
            (windowY is { } y && !double.IsFinite(y)))
        {
            return false;
        }

        snapshot = new BrowserPageViewSnapshot(
            page, screenX, screenY, windowX, windowY, width, height, isFocused);
        return true;
    }
}

/// <summary>
/// Retains immutable measured snapshots only; no NUI View or generated RPC object crosses this seam.
/// </summary>
public sealed class BrowserVisibleViewRegistry
{
    private readonly object _gate = new();
    private IReadOnlyList<BrowserPageViewSnapshot> _visible = [];

    public void Publish(BrowserPageViewSnapshot? snapshot)
    {
        lock (_gate)
        {
            _visible = snapshot is null ? [] : new[] { snapshot };
        }
    }

    public IReadOnlyList<BrowserPageViewSnapshot> GetAnnotatedViews()
    {
        lock (_gate)
        {
            return _visible.ToArray();
        }
    }

    public bool TryFind(string id, out BrowserPageViewSnapshot snapshot)
    {
        lock (_gate)
        {
            snapshot = _visible.FirstOrDefault(candidate => candidate.ViewId == id)!;
            return snapshot is not null;
        }
    }

    public bool TryGetFocused(out BrowserPageViewSnapshot snapshot)
    {
        lock (_gate)
        {
            snapshot = _visible.FirstOrDefault(candidate => candidate.IsFocused)!;
            return snapshot is not null;
        }
    }
}
