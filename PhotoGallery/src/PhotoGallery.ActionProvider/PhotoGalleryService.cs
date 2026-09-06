#nullable enable
using System.Text.Json;
using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryActionProvider;
using RPCPort.PhotoGalleryActionProvider.Stub;
namespace PhotoGallery.ActionProvider;

public sealed class PhotoGalleryService : TizenActionPhoto.ServiceBase
{
    private readonly GalleryLibraryService _service;
    public PhotoGalleryService() : this(PhotoGalleryActionProviderHost.Service) { }
    public PhotoGalleryService(GalleryLibraryService service) => _service = service;
    public override void OnCreate() { }
    public override void OnTerminate() { }
    public override TizenEntityStatus AddPhoto(TizenEntityPhoto photo) => Invoke(() =>
    {
        if (photo is null || photo.File is null || !string.IsNullOrEmpty(photo.Id))
            throw new ArgumentException("invalid: provide File.Path and an empty Photo.Id; MediaContent assigns the stable ID");
        var title = photo.Extra ?? "";
        // Extra in an Add request is the optional display title; returned Extra is versioned metadata.
        _service.ImportAsync(photo.File.Path, title, CancellationToken.None).GetAwaiter().GetResult();
    });
    public override TizenEntityStatus DeletePhoto(TizenEntityPhoto photo) => Invoke(() => _service.DeleteAsync(Id(photo), CancellationToken.None).GetAwaiter().GetResult());
    public override TizenEntityStatus GetCurrent(out TizenEntityPhoto result)
    {
        var value = EmptyPhoto(); var status = Invoke(() => value = ToEntity(_service.Current())); result = value; return status;
    }
    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityPhoto> result)
    {
        var values = new List<TizenEntityPhoto>();
        var status = Invoke(() =>
        {
            if (query is null) throw new ArgumentException("invalid: query is required");
            values = _service.Search(query.Id, query.Keyword, query.Category, query.Limit).Select(ToEntity).ToList();
        });
        result = values; return status;
    }
    public override TizenEntityStatus Show(TizenEntityPhoto photo) => Invoke(() => _service.Show(Id(photo)));
    public override TizenEntityStatus StartSlideshow() => Invoke(_service.StartSlideshow);
    public override TizenEntityStatus StopSlideshow() => Invoke(_service.StopSlideshow);
    public override TizenEntityStatus ToPresentation(TizenEntityPhoto photo, out TizenEntityPresentation result)
    {
        var value = new TizenEntityPresentation { Template = "", Document = "" };
        var status = Invoke(() => { var p = PhotoPresentation.Create(_service.Find(Id(photo))); value.Template = p.Template; value.Document = p.Document; });
        result = value; return status;
    }
    private static string Id(TizenEntityPhoto? photo) => photo?.Id ?? "";
    public static TizenEntityPhoto ToEntity(PhotoRecord p) => new()
    {
        Id = p.Id, Extra = JsonSerializer.Serialize(new { schemaVersion = 1, title = p.Title, album = p.Album, favorite = p.Favorite, owned = p.Owned }),
        Location = p.Location, Date = p.CapturedAt.ToString("O"), Note = p.Note,
        File = new TizenEntityFile { Id = p.Id, Extra = "", Path = p.Path, StorageType = p.StorageType, Size = (int)Math.Clamp(p.FileSize, 0, int.MaxValue), ModifiedDate = p.CapturedAt.ToString("O"), MimeType = p.MimeType },
    };
    public static TizenEntityPhoto EmptyPhoto() => new() { Id = "", Extra = "", Location = "", Date = "", Note = "", File = new TizenEntityFile { Id = "", Extra = "", Path = "", StorageType = "internal", ModifiedDate = "", MimeType = "" } };
    private static TizenEntityStatus Invoke(Action action)
    {
        try { action(); return new() { Success = true, Reason = "" }; }
        catch (Exception ex) { return new() { Success = false, Reason = GalleryProviderErrors.Describe(ex) }; }
    }
}

internal static class GalleryProviderErrors
{
    internal static string Describe(Exception ex) => ex switch
    {
        ArgumentException => "invalid: " + ex.Message.Split('\n')[0],
        InvalidOperationException => "unavailable: " + ex.Message,
        UnauthorizedAccessException => "unavailable: media access denied",
        FileNotFoundException => "not_found: the photo file is no longer available",
        IOException => "unavailable: media storage could not complete the operation",
        _ => "internal: photo operation failed (" + ex.GetType().Name + ")",
    };
}
