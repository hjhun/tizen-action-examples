#nullable enable
using ActionExamples.ViewAnnotations;
using PhotoGallery.ActionProvider;
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryViewActionProvider;
using RPCPort.PhotoGalleryViewActionProvider.Stub;

namespace PhotoGallery.ViewActionProvider;

public sealed class PhotoGalleryViewService : TizenActionView.ServiceBase
{
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus FindById(string id, out TizenEntityView v)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 1024) { v = EmptyView(); return Failure("invalid: view ID is required"); }
        if (PhotoGalleryViewProviderState.TryFind(id, out v)) return Success();
        v = EmptyView();
        return Failure("not_found: view is not currently visible");
    }

    public override TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = PhotoGalleryViewProviderState.GetAnnotatedViews();
        return views.Count > 0 ? Success() : Failure("unavailable: no rendered annotated surface is active");
    }

    public override TizenEntityStatus GetFocusedView(out TizenEntityView v)
    {
        if (PhotoGalleryViewProviderState.TryGetFocused(out v)) return Success();
        v = EmptyView();
        return Failure("not_found: no annotated view is focused");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityView view, out TizenEntityPresentation result)
    {
        result = new TizenEntityPresentation { Template = string.Empty, Document = string.Empty };
        if (view?.Annotation is not { } annotation || string.IsNullOrWhiteSpace(annotation.EntityId)) return Failure("A current annotated view is required.");
        var current = PhotoGalleryViewProviderState.Store.Resolve(view.Id, annotation.EntityType, annotation.EntityId);
        if (current is null) return Failure("The annotated view is no longer visible or its identity does not match.");
        string template, document;
        if (current.EntityType == "Tizen.Entity.Photo")
        {
            try
            {
                using var source = System.Text.Json.JsonDocument.Parse(current.EntityInfo);
                var photo = System.Text.Json.JsonSerializer.Deserialize<RPCPort.PhotoGalleryActionProvider.TizenEntityPhoto>(source.RootElement.GetProperty("TizenEntityPhoto").GetRawText(), new System.Text.Json.JsonSerializerOptions { IncludeFields = true })!;
                using var metadata = System.Text.Json.JsonDocument.Parse(photo.Extra);
                var extra = metadata.RootElement;
                var record = PhotoGallery.Domain.PhotoRecord.Create(photo.Id, extra.GetProperty("title").GetString(), DateTimeOffset.Parse(photo.Date), "", "", "") with
                { Album = extra.GetProperty("album").GetString() ?? "", Favorite = extra.GetProperty("favorite").GetBoolean(), Owned = extra.GetProperty("owned").GetBoolean() };
                (template, document) = PhotoPresentation.Create(record);
            }
            catch { return Failure("Invalid current photo annotation."); }
        }
        else if (!ViewSnapshotPresentation.TryCreate(current.EntityType, current.EntityId, current.EntityInfo, out template, out document))
            return Failure("Unsupported or invalid ViewAnnotation entity snapshot.");
        result.Template = template;
        result.Document = document;
        return Success();
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
    private static TizenEntityView EmptyView() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Type = string.Empty,
        Description = string.Empty,
        ScreenBounds = new ScreenBounds(),
        WindowBounds = new WindowBounds(),
        IsFocused = false,
        IsEnabled = false,
        Annotation = new Annotation { EntityId = string.Empty, EntityType = string.Empty, EntityInfo = string.Empty },
    };
}

internal static class PhotoGalleryViewProviderState
{
    internal static readonly CurrentViewStore Store = new();
    internal static List<TizenEntityView> GetAnnotatedViews() => Store.All().Select(ToView).ToList();
    internal static bool TryFind(string id, out TizenEntityView view)
    {
        var snapshot = Store.Find(id);
        view = snapshot is null ? new TizenEntityView() : ToView(snapshot);
        return snapshot is not null;
    }
    internal static bool TryGetFocused(out TizenEntityView view)
    {
        var snapshot = Store.Focused();
        view = snapshot is null ? new TizenEntityView() : ToView(snapshot);
        return snapshot is not null;
    }
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
