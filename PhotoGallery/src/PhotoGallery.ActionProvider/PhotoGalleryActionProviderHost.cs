#nullable enable

using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryActionProvider.Stub;

namespace PhotoGallery.ActionProvider;

public static class PhotoGalleryActionProviderHost
{
    private static TizenActionPhoto? _stub;

    public static void Start(IPhotoLibrary library, PhotoQueryService queries)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(queries);
        PhotoGalleryProviderState.Configure(library, queries);

        _stub ??= new TizenActionPhoto();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(PhotoGalleryService));
        }
    }
}

internal static class PhotoGalleryProviderState
{
    private static IPhotoLibrary _library = new UnavailablePhotoLibrary();
    private static PhotoQueryService _queries = new(_library);

    internal static IPhotoLibrary Library => _library;
    internal static PhotoQueryService Queries => _queries;

    internal static void Configure(IPhotoLibrary library, PhotoQueryService queries)
    {
        _library = library;
        _queries = queries;
    }

    private sealed class UnavailablePhotoLibrary : IPhotoLibrary
    {
        public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<PhotoRecord>>(
                new InvalidOperationException("The PhotoGallery provider has not been composed with a media library."));
    }
}
