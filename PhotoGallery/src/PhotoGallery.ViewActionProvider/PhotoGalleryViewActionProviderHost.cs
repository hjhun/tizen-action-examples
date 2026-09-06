#nullable enable
#nullable enable
using ActionExamples.ViewAnnotations;
using RPCPort.PhotoGalleryViewActionProvider.Stub;

namespace PhotoGallery.ViewActionProvider;

public static class PhotoGalleryViewActionProviderHost
{
    private static TizenActionView? _stub;
    public static void Start()
    {
        _stub ??= new TizenActionView();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(PhotoGalleryViewService));
    }
    public static void Publish(IEnumerable<CurrentViewSnapshot> views) => PhotoGalleryViewProviderState.Store.Publish(views);
    public static void ClearPublishedViews() => PhotoGalleryViewProviderState.Store.Clear();
}
