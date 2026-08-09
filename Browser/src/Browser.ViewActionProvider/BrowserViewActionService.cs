using Browser.Domain;
using Browser.UseCases;
using RPCPort.TizenInternalActionViewGenerated;
using RPCPort.TizenInternalActionViewGenerated.Stub;
using BrowserEntity = RPCPort.TizenActionBrowserGenerated.TizenEntityBrowser;

namespace Browser.ViewActionProvider;

/// <summary>
/// Maps the current visible Browser page to the generated internal View Action category.
/// The NUI root publishes only measured, visible normal-mode page snapshots.
/// </summary>
public sealed class BrowserViewActionService : TizenInternalActionView.ServiceBase
{
    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus FindById(string id, out TizenEntityView view)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            view = EmptyView();
            return Failure("invalid_input");
        }

        return BrowserViewProviderState.TryFind(id, out view)
            ? Success()
            : Failure("not_found");
    }

    public override RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = BrowserViewProviderState.GetAnnotatedViews();
        return Success();
    }

    public override RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus GetFocusedView(out TizenEntityView view)
    {
        return BrowserViewProviderState.TryGetFocused(out view)
            ? Success()
            : Failure("not_found");
    }

    public override RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus ToPresentation(
        TizenEntityView view,
        out RPCPort.TizenInternalActionViewGenerated.TizenEntityPresentation result)
    {
        result = EmptyPresentation();
        if (!BrowserViewProviderState.TryGetCurrentSnapshot(view, out var snapshot))
        {
            return Failure("invalid_input");
        }

        var presentation = BrowserActionContract.CreateLegacyDisplayPresentation(snapshot.Page);
        result = new TizenEntityPresentation
        {
            Template = presentation.Template,
            Document = presentation.Document,
        };
        return Success();
    }

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
        Annotation = new Annotation
        {
            EntityType = string.Empty,
            EntityId = string.Empty,
            EntityInfo = string.Empty,
        },
    };

    private static RPCPort.TizenInternalActionViewGenerated.TizenEntityPresentation EmptyPresentation() => new()
    {
        Template = string.Empty,
        Document = string.Empty,
    };

    private static RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static RPCPort.TizenInternalActionViewGenerated.TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}

internal static class BrowserViewProviderState
{
    internal const string BrowserEntityType = "Tizen.Entity.Browser";
    private static readonly BrowserVisibleViewRegistry Registry = new();

    internal static void PublishVisiblePage(BrowserPageViewSnapshot? snapshot)
    {
        Registry.Publish(snapshot);
    }

    internal static bool TryFind(string id, out TizenEntityView view)
    {
        var found = Registry.TryFind(id, out var snapshot);
        view = found ? ToAnnotatedView(snapshot) : EmptyView();
        return found;
    }

    internal static List<TizenEntityView> GetAnnotatedViews()
    {
        return Registry.GetAnnotatedViews().Select(ToAnnotatedView).ToList();
    }

    internal static bool TryGetFocused(out TizenEntityView view)
    {
        var found = Registry.TryGetFocused(out var snapshot);
        view = found ? ToAnnotatedView(snapshot) : EmptyView();
        return found;
    }

    internal static bool TryGetCurrentSnapshot(TizenEntityView? requested, out BrowserPageViewSnapshot snapshot)
    {
        snapshot = null!;
        if (requested?.Annotation is null ||
            requested.Annotation.EntityType != BrowserEntityType ||
            string.IsNullOrWhiteSpace(requested.Id))
        {
            return false;
        }

        if (Registry.TryFind(requested.Id, out var current))
        {
            var generated = ToAnnotatedView(current);
            if (requested.Annotation.EntityId != generated.Annotation.EntityId ||
                requested.Annotation.EntityInfo != generated.Annotation.EntityInfo)
            {
                return false;
            }

            snapshot = current;
            return true;
        }

        return false;
    }

    private static TizenEntityView ToAnnotatedView(BrowserPageViewSnapshot snapshot)
    {
        var page = snapshot.Page;
        var windowBounds = snapshot.WireWindowBounds;
        var entity = new BrowserEntity
        {
            Id = page.Id,
            Extra = string.Empty,
            Url = page.Url,
            Title = page.Title,
            Details = page.Details,
        };

        return new TizenEntityView
        {
            Id = snapshot.ViewId,
            Extra = string.Empty,
            Type = "Browser.WebView",
            Description = page.Title,
            ScreenBounds = new ScreenBounds
            {
                X = snapshot.ScreenX,
                Y = snapshot.ScreenY,
                Width = snapshot.Width,
                Height = snapshot.Height,
            },
            WindowBounds = new WindowBounds
            {
                X = windowBounds.X,
                Y = windowBounds.Y,
                Width = windowBounds.Width,
                Height = windowBounds.Height,
            },
            IsFocused = snapshot.IsFocused,
            IsEnabled = true,
            Annotation = new Annotation
            {
                EntityType = BrowserEntityType,
                EntityId = page.Id,
                EntityInfo = entity.ToJson(),
            },
        };
    }

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
        Annotation = new Annotation
        {
            EntityType = string.Empty,
            EntityId = string.Empty,
            EntityInfo = string.Empty,
        },
    };
}
