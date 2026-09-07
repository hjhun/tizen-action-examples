#nullable enable

using Browser.UseCases;
using RPCPort.TizenActionBrowser.Stub;

namespace Browser.ActionProvider;

/// <summary>
/// Starts the generated Browser Action listener with the same query and navigation services
/// used by the NUI application.
/// </summary>
public static class BrowserActionProviderHost
{
    private static TizenActionBrowser? _stub;
    private static RPCPort.BrowserCustomActions.Stub.TizenActionBrowserCustom? _custom;

    public static void Start(BrowserPageQueryService queries, IBrowserActionNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(navigation);
        BrowserActionProviderState.Configure(queries, navigation);

        _custom ??= new RPCPort.BrowserCustomActions.Stub.TizenActionBrowserCustom();
        if (!_custom.GetListenStatus()) _custom.Listen(typeof(BrowserCustomActionService));

        _stub ??= new TizenActionBrowser();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(BrowserActionService));
        }
    }
}

internal static class BrowserActionProviderState
{
    private static BrowserPageQueryService? _queries;
    private static IBrowserActionNavigation? _navigation;

    internal static BrowserPageQueryService Queries =>
        _queries ?? throw new InvalidOperationException("Browser Action provider has not been configured.");

    internal static IBrowserActionNavigation Navigation =>
        _navigation ?? throw new InvalidOperationException("Browser Action provider has not been configured.");

    internal static void Configure(BrowserPageQueryService queries, IBrowserActionNavigation navigation)
    {
        _queries = queries;
        _navigation = navigation;
    }
}
