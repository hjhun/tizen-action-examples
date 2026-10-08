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
    public override TizenEntityStatus AddPhoto(TizenEntityPhoto photo, out TizenEntityPhoto result)
    {
        result = null!;
        return Unavailable();
    }
    public override TizenEntityStatus DeletePhoto(List<TizenEntityPhoto> photos) => Unavailable();
    public override TizenEntityStatus GetCurrent(out TizenEntityPhoto result)
    {
        var value = EmptyPhoto(); var status = Invoke(() => value = ToEntity(_service.Current())); result = value; return status;
    }
    public override TizenEntityStatus Search(TizenEntityPhotoQuery query, out List<TizenEntityPhoto> result)
    {
        var values = new List<TizenEntityPhoto>();
        var status = Invoke(() =>
        {
            if (query is null) throw new ArgumentException("invalid: query is required");
            if (query.DateFrom is not null || query.DateTo is not null ||
                query.Location is not null || query.Person is not null ||
                query.Activity is not null || query.Object is not null)
                throw new InvalidOperationException("photo filters are not implemented");
            values = _service.Search(query.Id, query.Keyword, query.Category, query.Limit ?? 0).Select(ToEntity).ToList();
        });
        result = values; return status;
    }
    public override TizenEntityStatus Show(TizenEntityPhoto photo) => Invoke(() => _service.Show(Id(photo)));
    public override TizenEntityStatus StartSlideshow(List<TizenEntityPhoto> photos, out List<TizenEntityPhoto> result)
    {
        result = [];
        return Unavailable();
    }
    public override TizenEntityStatus StopSlideshow() => Invoke(_service.StopSlideshow);
    public override TizenEntityStatus GetMemories(out TizenEntityMemoryFeed result)
    {
        result = new() { Memories = [], RecommendationIndex = -1, Locked = false };
        return Unavailable();
    }
    public override TizenEntityStatus PlayMemory(TizenEntityMemory memory) => Unavailable();
    private static TizenEntityStatus Unavailable() => new()
    {
        Success = false, Reason = "unavailable: canonical action is not implemented",
    };
    private static string Id(TizenEntityPhoto? photo) => photo?.Id ?? "";
    public static TizenEntityPhoto ToEntity(PhotoRecord p) => new()
    {
        Id = p.Id, Extra = JsonSerializer.Serialize(new { schemaVersion = 1, title = p.Title, album = p.Album, favorite = p.Favorite, owned = p.Owned }),
        Location = p.Location, Date = p.CapturedAt.ToString("O"), Note = p.Note,
        File = new TizenEntityFile { Id = p.Id, Extra = "", Path = p.Path, StorageType = p.StorageType, Size = (int)Math.Clamp(p.FileSize, 0, int.MaxValue), ModifiedDate = p.CapturedAt.ToString("O") },
    };
    public static TizenEntityPhoto EmptyPhoto() => new() { Id = "", Extra = "", Location = "", Date = "", Note = "", File = new TizenEntityFile { Id = "", Extra = "", Path = "", StorageType = "internal", ModifiedDate = "" } };
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
