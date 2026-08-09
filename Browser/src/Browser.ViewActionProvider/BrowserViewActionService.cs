using System.Text.Json;
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
        if (view?.Annotation is null ||
            view.Annotation.EntityType != BrowserViewProviderState.BrowserEntityType ||
            !TryCreatePresentation(view.Annotation.EntityInfo, out var presentation))
        {
            return Failure("invalid_input");
        }

        result = new TizenEntityPresentation
        {
            Template = presentation.Template,
            Document = presentation.Document,
        };
        return Success();
    }

    private static bool TryCreatePresentation(string? entityInfo, out BrowserPresentation presentation)
    {
        presentation = null!;
        if (string.IsNullOrWhiteSpace(entityInfo))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(entityInfo);
            if (!json.RootElement.TryGetProperty("TizenEntityBrowser", out var browser) ||
                !browser.TryGetProperty("Id", out var id) ||
                !browser.TryGetProperty("Url", out var url) ||
                !browser.TryGetProperty("Title", out var title) ||
                !browser.TryGetProperty("Details", out var details) ||
                !BrowserActionContract.TryCreatePage(
                    id.GetString(), url.GetString(), title.GetString(), details.GetString(), out var page))
            {
                return false;
            }

            presentation = BrowserActionContract.CreatePresentation(page);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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
    private static readonly object Gate = new();
    private static IReadOnlyList<TizenEntityView> _visibleViews = [];

    internal static void PublishVisiblePage(BrowserPageViewSnapshot? snapshot)
    {
        IReadOnlyList<TizenEntityView> published = snapshot is not null && IsValid(snapshot)
            ? new[] { ToAnnotatedView(snapshot) }
            : Array.Empty<TizenEntityView>();
        lock (Gate)
        {
            _visibleViews = published;
        }
    }

    internal static bool TryFind(string id, out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _visibleViews.FirstOrDefault(candidate => candidate.Id == id) ?? EmptyView();
            return !string.IsNullOrEmpty(view.Id);
        }
    }

    internal static List<TizenEntityView> GetAnnotatedViews()
    {
        lock (Gate)
        {
            return _visibleViews.ToList();
        }
    }

    internal static bool TryGetFocused(out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _visibleViews.FirstOrDefault(candidate => candidate.IsFocused) ?? EmptyView();
            return !string.IsNullOrEmpty(view.Id);
        }
    }

    private static bool IsValid(BrowserPageViewSnapshot snapshot) =>
        double.IsFinite(snapshot.ScreenX) && double.IsFinite(snapshot.ScreenY) &&
        double.IsFinite(snapshot.Width) && double.IsFinite(snapshot.Height) &&
        snapshot.Width > 0 && snapshot.Height > 0 &&
        (snapshot.WindowX is null || double.IsFinite(snapshot.WindowX.Value)) &&
        (snapshot.WindowY is null || double.IsFinite(snapshot.WindowY.Value));

    private static TizenEntityView ToAnnotatedView(BrowserPageViewSnapshot snapshot)
    {
        var page = snapshot.Page;
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
            Id = $"browser:page:{page.Id}",
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
            WindowBounds = snapshot.WindowX is { } windowX && snapshot.WindowY is { } windowY
                ? new WindowBounds
                {
                    X = windowX,
                    Y = windowY,
                    Width = snapshot.Width,
                    Height = snapshot.Height,
                }
                : null!,
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
