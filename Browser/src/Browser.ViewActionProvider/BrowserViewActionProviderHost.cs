using Browser.UseCases;
using RPCPort.TizenActionView.Stub;

namespace Browser.ViewActionProvider;

/// <summary>
/// Owns the generated View listener and the current visible Browser-page snapshot.
/// </summary>
public static class BrowserViewActionProviderHost
{
    private static TizenActionView? _stub;

    public static void Start()
    {
        _stub ??= new TizenActionView();
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
