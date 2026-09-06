#nullable enable
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryActionProvider.Stub;
using RPCPort.PhotoGalleryCustomActionProvider.Stub;
namespace PhotoGallery.ActionProvider;
public static class PhotoGalleryActionProviderHost
{
    private static TizenActionPhoto? _photo;
    private static TizenActionPhotoGalleryCustom? _custom;
    internal static GalleryLibraryService Service { get; private set; } = null!;
    public static void Start(GalleryLibraryService service)
    {
        Service = service;
        _photo ??= new(); _custom ??= new();
        if (!_photo.GetListenStatus()) _photo.Listen(typeof(PhotoGalleryService));
        if (!_custom.GetListenStatus()) _custom.Listen(typeof(PhotoGalleryCustomService));
    }
}
