#nullable enable
namespace ActionExamples.ViewAnnotations;

// Immutable, Tizen-independent publication state shared by the two View providers.
public sealed record CurrentViewSnapshot(
    string Id, string Type, string Description,
    string EntityType, string EntityId, string EntityInfo)
{
    public double ScreenX { get; init; }
    public double ScreenY { get; init; }
    public double? WindowX { get; init; }
    public double? WindowY { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsFocused { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public sealed class CurrentViewStore
{
    private readonly object _gate = new();
    private CurrentViewSnapshot[] _views = [];

    public void Publish(IEnumerable<CurrentViewSnapshot> snapshots)
    {
        var views = snapshots.Where(x =>
                !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.EntityId) &&
                !string.IsNullOrWhiteSpace(x.EntityType) && !string.IsNullOrWhiteSpace(x.EntityInfo) &&
                double.IsFinite(x.ScreenX) && double.IsFinite(x.ScreenY) &&
                double.IsFinite(x.Width) && double.IsFinite(x.Height) && x.Width > 0 && x.Height > 0)
            .GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).ToArray();
        // A frame replaces its predecessor, including when it has no measured views yet.
        lock (_gate) _views = views;
    }

    public IReadOnlyList<CurrentViewSnapshot> All() { lock (_gate) return _views.ToArray(); }
    public CurrentViewSnapshot? Find(string id) { lock (_gate) return _views.FirstOrDefault(x => x.Id == id); }
    public CurrentViewSnapshot? Resolve(string? id, string? entityType, string? entityId)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 1024) return null;
        lock (_gate) return _views.FirstOrDefault(x => x.Id == id && x.EntityType == entityType && x.EntityId == entityId);
    }
    public CurrentViewSnapshot? Focused() { lock (_gate) return _views.FirstOrDefault(x => x.IsFocused); }
    public void Clear() => Publish([]);
}
