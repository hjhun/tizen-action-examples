#nullable enable

using Calendar.Domain;
using ActionExamples.ViewAnnotations;
using RPCPort.CalendarViewActionProvider;
using RPCPort.CalendarViewActionProvider.Stub;

namespace Calendar.ViewActionProvider;

public sealed class CalendarViewService : TizenActionView.ServiceBase
{
    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus FindById(string id, out TizenEntityView view)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 1024)
        {
            view = CalendarViewProviderState.EmptyView();
            return Failure("A view ID is required.");
        }

        return CalendarViewProviderState.TryFind(id, out view)
            ? Success()
            : Failure("The requested view is not currently visible.");
    }

    public override TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = CalendarViewProviderState.GetAnnotatedViews();
        return Success();
    }

    public override TizenEntityStatus GetFocusedView(out TizenEntityView view)
    {
        return CalendarViewProviderState.TryGetFocused(out view)
            ? Success()
            : Failure("No annotated calendar view is currently focused.");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityView view, out TizenEntityPresentation result)
    {
        result = new TizenEntityPresentation { Template = string.Empty, Document = string.Empty };
        if (view?.Annotation is not { } annotation || string.IsNullOrWhiteSpace(annotation.EntityId)) return Failure("A current annotated view is required.");
        var current = CalendarViewProviderState.Store.Resolve(view.Id, annotation.EntityType, annotation.EntityId);
        if (current is null) return Failure("The annotated view is no longer visible or its identity does not match.");
        if (current.EntityType == "Tizen.Entity.CalendarEvent" &&
            CalendarA2UiPresentations.TryCreateFromGeneratedEntityJson(current.EntityInfo, out var eventPresentation, current.EntityId))
        {
            result.Template = eventPresentation.Template;
            result.Document = eventPresentation.Document;
            return Success();
        }
        if (!ViewSnapshotPresentation.TryCreate(current.EntityType, current.EntityId, current.EntityInfo, out var template, out var document))
            return Failure("Unsupported or invalid ViewAnnotation entity snapshot.");
        result.Template = template;
        result.Document = document;
        return Success();
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}

internal static class CalendarViewProviderState
{
    internal static readonly CurrentViewStore Store = new();
    internal static List<TizenEntityView> GetAnnotatedViews() => Store.All().Select(ToView).ToList();
    internal static bool TryFind(string id, out TizenEntityView view)
    {
        var snapshot = Store.Find(id);
        view = snapshot is null ? CalendarViewProviderState.EmptyView() : ToView(snapshot);
        return snapshot is not null;
    }
    internal static bool TryGetFocused(out TizenEntityView view)
    {
        var snapshot = Store.Focused();
        view = snapshot is null ? CalendarViewProviderState.EmptyView() : ToView(snapshot);
        return snapshot is not null;
    }
    internal static TizenEntityView EmptyView() => new()
    {
        Id = string.Empty, Extra = string.Empty, Type = string.Empty, Description = string.Empty,
        ScreenBounds = new ScreenBounds(), WindowBounds = new WindowBounds(),
        Annotation = new Annotation { EntityId = string.Empty, EntityType = string.Empty, EntityInfo = string.Empty },
    };
    private static TizenEntityView ToView(CurrentViewSnapshot snapshot) => new()
    {
        Id = snapshot.Id, Extra = string.Empty, Type = snapshot.Type, Description = snapshot.Description,
        ScreenBounds = new ScreenBounds { X = snapshot.ScreenX, Y = snapshot.ScreenY, Width = snapshot.Width, Height = snapshot.Height },
        WindowBounds = snapshot.WindowX is { } x && snapshot.WindowY is { } y && double.IsFinite(x) && double.IsFinite(y)
            ? new WindowBounds { X = x, Y = y, Width = snapshot.Width, Height = snapshot.Height } : new WindowBounds(),
        IsFocused = snapshot.IsFocused, IsEnabled = snapshot.IsEnabled,
        Annotation = new Annotation { EntityType = snapshot.EntityType, EntityId = snapshot.EntityId, EntityInfo = snapshot.EntityInfo },
    };
}
