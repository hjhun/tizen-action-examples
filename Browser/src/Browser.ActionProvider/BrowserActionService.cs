using Browser.Domain;
using Browser.UseCases;
using RPCPort.TizenActionBrowser;
using RPCPort.TizenActionBrowser.Stub;

namespace Browser.ActionProvider;

/// <summary>
/// Thin generated-category adapter. The NUI composition supplies the navigation bridge so this
/// synchronous TIDL callback never blocks the provider thread on WebView work.
/// </summary>
public sealed class BrowserActionService : TizenActionBrowser.ServiceBase
{

    private readonly BrowserPageQueryService _queries;

    public BrowserActionService()
        : this(BrowserActionProviderState.Queries)
    {
    }

    public BrowserActionService(BrowserPageQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus GetCurrentPage(out TizenEntityWebPageInfo result)
    {
        var snapshot = _queries.GetCurrentSnapshot();
        result = snapshot.Page is null ? EmptyBrowser() : ToEntity(snapshot.Page);
        return snapshot.Page is null ? Failure(CurrentFailureReason(snapshot.Surface)) : Success();
    }

    public override TizenEntityStatus ToPresentation(TizenEntityWebPageInfo browser, out TizenEntityPresentation result)
    {
        result = EmptyPresentation();
        if (!TryToPage(browser, out var requested))
        {
            return Failure("invalid_input");
        }

        var snapshot = _queries.GetCurrentSnapshot();
        if (snapshot.Page is not { } current)
        {
            return Failure(CurrentFailureReason(snapshot.Surface));
        }
        if (!BrowserActionContract.RepresentsSamePage(requested, current))
        {
            return Failure("not_current");
        }

        // Tizen.Entity.Presentation currently carries the repository's explicitly named legacy
        // Template/Document pair. The canonical v0.9.1 stream is produced separately by the
        // portable contract until a negotiated canonical Presentation transport exists.
        var presentation = BrowserActionContract.CreateLegacyDisplayPresentation(current);
        result = new TizenEntityPresentation
        {
            Template = presentation.Template,
            Document = presentation.Document
        };
        return Success();
    }

    public override TizenEntityStatus GetTabs(out List<TizenEntityTab> result)
    {
        result = new();
        var workspace = _queries.GetTabsSnapshot();
        if (workspace is null) return Failure("unavailable: tabs_not_ready");
        result = workspace.Tabs.Select((tab, index) => new TizenEntityTab
        {
            Id = tab.Id, Extra = string.Empty, Ordinal = index + 1,
            Focused = tab.Id == workspace.SelectedTabId, Secret = false,
            Page = tab.Page is { } page ? ToEntity(page) : new TizenEntityWebPageInfo
            { Id = tab.Id, Extra = string.Empty, Url = string.Empty, Title = "New tab", Details = string.Empty }
        }).ToList();
        return Success();
    }

    // P1 intermediate contract: these complete-category slots are not advertised.
    private static TizenEntityStatus NotEnabled() => Failure("unavailable: capability_not_enabled");
    public override TizenEntityStatus ControlMedia(TizenEntityBrowserMediaCommand input) => NotEnabled();
    public override TizenEntityStatus Engage(TizenEntityEngageCommand input) => NotEnabled();
    public override TizenEntityStatus Exit() => NotEnabled();
    public override TizenEntityStatus GoToScreen(TizenEntityBrowserScreen input) => NotEnabled();
    public override TizenEntityStatus Navigate(TizenEntityNavigationCommand input) => NotEnabled();
    public override TizenEntityStatus OpenPage(TizenEntityWebPageInfo input) => NotEnabled();
    public override TizenEntityStatus SelectItem(TizenEntityBrowserSelection input) => NotEnabled();
    public override TizenEntityStatus SetMediaOption(TizenEntityBrowserMediaOption input) => NotEnabled();
    public override TizenEntityStatus SetScreenOption(TizenEntityBrowserScreenOption input) => NotEnabled();
    public override TizenEntityStatus ControlTab(TizenEntityTabCommand input, out TizenEntityTab result)
    {
        result = new() { Id = "", Extra = "", Page = EmptyBrowser() };
        return NotEnabled();
    }
    public override TizenEntityStatus GetMedia(out TizenEntityBrowserMedia result)
    {
        result = new() { Id = "", Extra = "", Title = "" };
        return NotEnabled();
    }
    public override TizenEntityStatus Search(TizenEntityBrowserQuery input, out TizenEntityBrowserSearchResult result)
    {
        result = new() { Id = "", Extra = "", Answer = new(), Place = new(), Product = new(), Person = new() };
        return NotEnabled();
    }
    public override TizenEntityStatus SearchPageList(TizenEntityQuery input, out List<TizenEntityPageListItem> result)
    {
        result = new();
        return NotEnabled();
    }
    public override TizenEntityStatus ToCalendar(TizenEntityWebPageInfo input, out TizenEntityCalendarEvent result)
    {
        result = EmptyCalendar();
        return NotEnabled();
    }
    public override TizenEntityStatus UpdatePageList(TizenEntityPageListCommand input, out TizenEntityPageListItem result)
    {
        result = new() { Id = "", Extra = "", List = "", Date = "", Page = EmptyBrowser() };
        return NotEnabled();
    }

    private static bool TryToPage(TizenEntityWebPageInfo? entity, out BrowserPage page)
    {
        return BrowserActionContract.TryCreatePage(
            entity?.Id, entity?.Url, entity?.Title, entity?.Details, out page);
    }

    private static TizenEntityWebPageInfo ToEntity(BrowserPage page) => new()
    {
        Id = page.Id,
        Extra = string.Empty,
        Url = page.Url,
        Title = page.Title,
        Details = page.Details
    };

    private static TizenEntityWebPageInfo EmptyBrowser() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Url = string.Empty,
        Title = string.Empty,
        Details = string.Empty
    };

    private static TizenEntityCalendarEvent EmptyCalendar() => new()
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

    private static string CurrentFailureReason(BrowserAgentSurface surface) =>
        surface == BrowserAgentSurface.Home ? "not_found" : "unavailable";
}

public interface IBrowserActionNavigation
{
    bool RequestNavigation(BrowserPage page);
}
