#nullable enable
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryCustomActionProvider;
using RPCPort.PhotoGalleryCustomActionProvider.Stub;
namespace PhotoGallery.ActionProvider;

public sealed class PhotoGalleryCustomService : TizenActionPhotoGalleryCustom.ServiceBase
{
    private readonly GalleryLibraryService _service;
    public PhotoGalleryCustomService() : this(PhotoGalleryActionProviderHost.Service) { }
    public PhotoGalleryCustomService(GalleryLibraryService service) => _service = service;
    public override void OnCreate() { }
    public override void OnTerminate() { }
    public override TizenEntityStatus GetPhotoByIds(List<string> ids, out List<TizenEntityPhoto> result, out List<string> unresolvedIds)
    {
        result = []; unresolvedIds = [];
        try
        {
            var resolution = _service.Resolve(ids);
            result = resolution.Photos.Select(p => { var dto = PhotoGalleryService.ToEntity(p); return new TizenEntityPhoto { Id = dto.Id, Extra = dto.Extra, Date = dto.Date, Note = dto.Note, Location = dto.Location, File = new TizenEntityFile { Id = dto.File.Id, Extra = dto.File.Extra, Path = dto.File.Path, StorageType = dto.File.StorageType, Size = dto.File.Size, ModifiedDate = dto.File.ModifiedDate, MimeType = dto.File.MimeType } }; }).ToList();
            unresolvedIds = resolution.UnresolvedIds.ToList();
            return new() { Success = true, Reason = "" };
        }
        catch (Exception ex) { return new() { Success = false, Reason = GalleryProviderErrors.Describe(ex) }; }
    }
    public override TizenEntityStatus SetFavorite(string id, bool favorite)
    {
        try { _service.SetFavoriteAsync(id, favorite, CancellationToken.None).GetAwaiter().GetResult(); return new() { Success = true, Reason = "" }; }
        catch (Exception ex) { return new() { Success = false, Reason = GalleryProviderErrors.Describe(ex) }; }
    }
}
