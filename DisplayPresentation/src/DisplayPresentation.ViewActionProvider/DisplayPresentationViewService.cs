using RPCPort.ViewActions;
using RPCPort.ViewActions.Stub;

namespace DisplayPresentation.ViewActionProvider;

/// <summary>
/// Whole-category compile probe. View publication starts with the NUI renderer,
/// so every unimplemented route returns a typed status and initialized graph.
/// </summary>
public sealed class DisplayPresentationViewService : TizenInternalActionView.ServiceBase
{
    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus FindById(string id, out TizenEntityView v)
    {
        v = EmptyView();
        return Failure("No rendered presentation view is currently available.");
    }

    public override TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = [];
        return Success();
    }

    public override TizenEntityStatus GetFocusedView(out TizenEntityView v)
    {
        v = EmptyView();
        return Failure("No annotated presentation view is currently focused.");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityView view, out TizenEntityPresentation result)
    {
        result = new TizenEntityPresentation { Template = string.Empty, Document = string.Empty };
        return Failure("No rendered presentation view is currently available.");
    }

    private static TizenEntityView EmptyView() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Type = string.Empty,
        Description = string.Empty,
        ScreenBounds = new ScreenBounds(),
        WindowBounds = new WindowBounds(),
        Annotation = new Annotation { EntityType = string.Empty, EntityId = string.Empty, EntityInfo = string.Empty },
    };

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
