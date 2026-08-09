using Browser.Domain;
using Browser.UseCases;
using RPCPort.TizenActionBrowserGenerated;
using RPCPort.TizenActionBrowserGenerated.Stub;

namespace Browser.ActionProvider;

/// <summary>
/// Thin generated-category adapter. The NUI composition supplies the navigation bridge so this
/// synchronous TIDL callback never blocks the provider thread on WebView work.
/// </summary>
public sealed class BrowserActionService : TizenActionBrowser.ServiceBase
{

    private readonly BrowserPageQueryService _queries;
    private readonly IBrowserActionNavigation _navigation;

    public BrowserActionService()
        : this(BrowserActionProviderState.Queries, BrowserActionProviderState.Navigation)
    {
    }

    public BrowserActionService(BrowserPageQueryService queries, IBrowserActionNavigation navigation)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus GetCurrent(out TizenEntityBrowser result)
    {
        var current = _queries.GetCurrentPage();
        result = current is null ? EmptyBrowser() : ToEntity(current);
        return current is null ? Failure("not_found") : Success();
    }

    public override TizenEntityStatus Go(TizenEntityBrowser browser)
    {
        if (!TryToPage(browser, out var page))
        {
            return Failure("invalid_input");
        }

        return _navigation.RequestNavigation(page) ? Success() : Failure("unavailable");
    }

    public override TizenEntityStatus ToCalendar(TizenEntityBrowser browser, out TizenEntityCalendar result)
    {
        result = EmptyCalendar();
        return TryToPage(browser, out _) ? Failure("unavailable") : Failure("invalid_input");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityBrowser browser, out TizenEntityPresentation result)
    {
        result = EmptyPresentation();
        if (!TryToPage(browser, out var page))
        {
            return Failure("invalid_input");
        }

        var presentation = BrowserActionContract.CreatePresentation(page);
        result = new TizenEntityPresentation
        {
            Template = presentation.Template,
            Document = presentation.Document
        };
        return Success();
    }

    public override TizenEntityStatus GetBrowserByIds(
        List<string> ids,
        out List<TizenEntityBrowser> result,
        out List<string> unresolvedIds)
    {
        result = new List<TizenEntityBrowser>();
        unresolvedIds = new List<string>();
        if (!BrowserActionContract.HasValidResolverIds(ids))
        {
            return Failure("invalid_input");
        }

        var resolution = _queries.ResolveByIds(ids);
        result = resolution.Pages.Select(ToEntity).ToList();
        unresolvedIds = resolution.UnresolvedIds.ToList();
        return Success();
    }

    private static bool TryToPage(TizenEntityBrowser? entity, out BrowserPage page)
    {
        return BrowserActionContract.TryCreatePage(
            entity?.Id, entity?.Url, entity?.Title, entity?.Details, out page);
    }

    private static TizenEntityBrowser ToEntity(BrowserPage page) => new()
    {
        Id = page.Id,
        Extra = string.Empty,
        Url = page.Url,
        Title = page.Title,
        Details = page.Details
    };

    private static TizenEntityBrowser EmptyBrowser() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Url = string.Empty,
        Title = string.Empty,
        Details = string.Empty
    };

    private static TizenEntityCalendar EmptyCalendar() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Title = string.Empty,
        StartDate = string.Empty,
        EndDate = string.Empty,
        Note = string.Empty,
        Location = string.Empty
    };

    private static TizenEntityPresentation EmptyPresentation() => new()
    {
        Template = string.Empty,
        Document = string.Empty
    };

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}

public interface IBrowserActionNavigation
{
    bool RequestNavigation(BrowserPage page);
}
