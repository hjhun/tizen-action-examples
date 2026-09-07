using Browser.UseCases;
using RPCPort.BrowserCustomActions;

namespace Browser.ActionProvider;

public sealed class BrowserCustomActionService : RPCPort.BrowserCustomActions.Stub.TizenActionBrowserCustom.ServiceBase
{
    private readonly BrowserPageQueryService _queries;
    public BrowserCustomActionService() : this(BrowserActionProviderState.Queries) { }
    public BrowserCustomActionService(BrowserPageQueryService queries) => _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus GetPageByIds(List<string> ids, out List<TizenEntityWebPageInfo> result, out List<string> unresolvedIds)
    {
        result = new();
        unresolvedIds = new();
        if (!BrowserActionContract.HasValidResolverIds(ids))
            return new() { Success = false, Reason = "invalid_input" };
        var resolution = _queries.ResolveByIds(ids);
        result = resolution.Pages.Select(page => new TizenEntityWebPageInfo
        {
            Id = page.Id, Extra = "", Url = page.Url, Title = page.Title, Details = page.Details
        }).ToList();
        unresolvedIds = resolution.UnresolvedIds.ToList();
        return new() { Success = true, Reason = "" };
    }
}
