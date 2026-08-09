using Browser.UseCases;
using RPCPort.TizenInternalActionView.Stub;

namespace Browser.ViewActionProvider;

/// <summary>
/// Owns the generated View listener and the current visible Browser-page snapshot.
/// </summary>
public static class BrowserViewActionProviderHost
{
    private static TizenInternalActionView? _stub;

    public static void Start()
    {
        _stub ??= new TizenInternalActionView();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(BrowserViewActionService));
        }
    }

    public static void PublishVisiblePage(BrowserPageViewSnapshot? snapshot) =>
        BrowserViewProviderState.PublishVisiblePage(snapshot);

    public static void ClearPublishedViews() =>
        BrowserViewProviderState.PublishVisiblePage(null);
}
