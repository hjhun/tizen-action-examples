#nullable enable

using System.Text.Json;
using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using RPCPort.PhotoGalleryActionProvider;
using RPCPort.PhotoGalleryActionProvider.Stub;

namespace PhotoGallery.ActionProvider;

/// <summary>
/// Generated Action adapter. The synchronous TIDL surface is bounded to a
/// background-capable Action call; UI code uses the same <see cref="PhotoQueryService"/>
/// and never calls this provider through RPC.
/// </summary>
public sealed class PhotoGalleryService : TizenActionPhoto.ServiceBase
{
    private static readonly TimeSpan ProviderReadTimeout = TimeSpan.FromSeconds(5);
    private readonly IPhotoLibrary _library;
    private readonly PhotoQueryService _queries;

    public PhotoGalleryService()
        : this(PhotoGalleryProviderState.Library, PhotoGalleryProviderState.Queries)
    {
    }

    public PhotoGalleryService(IPhotoLibrary library, PhotoQueryService queries)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus AddImage(TizenEntityPhoto photo) =>
        Failure("Adding media is unavailable until the target MediaContent registration capability is verified.");

    public override TizenEntityStatus DeleteImage(TizenEntityPhoto photo) =>
        Failure("Deleting media is unavailable until the target MediaContent mutation capability is verified.");

    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityPhoto> result)
    {
        result = [];
        if (query is null || query.Keyword?.Length > PhotoSearchCriteria.MaximumKeywordLength ||
            query.Number > PhotoSearchCriteria.MaximumResultCount || query.Number < 0 ||
            !string.IsNullOrEmpty(query.Category))
        {
            return Failure("Search requires a bounded keyword, a result count from 0 to 200, and no category filter.");
        }

        try
        {
            using var cancellation = new CancellationTokenSource(ProviderReadTimeout);
            var criteria = PhotoSearchCriteria.Create(query.Keyword, null, null, query.Number == 0 ? 20 : query.Number);
            result = _queries.SearchAsync(criteria, cancellation.Token).GetAwaiter().GetResult().Photos.Select(ToEntity).ToList();
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Failure("The media library did not respond before the bounded provider timeout.");
        }
        catch (Exception exception)
        {
            return Failure($"The media library is unavailable: {exception.Message}");
        }
    }

    public override TizenEntityStatus GetPhotoByIds(
        List<string> ids,
        out List<TizenEntityPhoto> result,
        out List<string> unresolvedIds)
    {
        result = [];
        unresolvedIds = [];
        if (ids is null || ids.Count > PhotoResolver.MaximumIdsPerRequest ||
            ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > PhotoRecord.MaximumIdLength))
        {
            return Failure("ids must contain at most 100 non-empty stable IDs, each no longer than 256 characters.");
        }

        try
        {
            var resolution = PhotoResolver.ResolveByIds(ReadSnapshot(), ids);
            result = resolution.Photos.Select(ToEntity).ToList();
            unresolvedIds = resolution.UnresolvedIds.ToList();
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Failure("The media library did not respond before the bounded provider timeout.");
        }
        catch (Exception exception)
        {
            return Failure($"The media library is unavailable: {exception.Message}");
        }
    }

    public override TizenEntityStatus ToPresentation(TizenEntityPhoto photo, out TizenEntityPresentation result)
    {
        result = new TizenEntityPresentation { Template = string.Empty, Document = string.Empty };
        if (photo is null || string.IsNullOrWhiteSpace(photo.Id) || photo.Id.Length > PhotoRecord.MaximumIdLength)
        {
            return Failure("A stable photo ID is required.");
        }

        try
        {
            var resolved = PhotoResolver.ResolveByIds(ReadSnapshot(), [photo.Id]);
            if (resolved.Photos.Count == 0)
            {
                return Failure("The requested photo is no longer available.");
            }

            var currentEntity = ToEntity(resolved.Photos[0]);
            var entityInfo = currentEntity.ToJson();
            result = new TizenEntityPresentation
            {
                Template = JsonSerializer.Serialize(new
                {
                    surfaceUpdate = new
                    {
                        surfaceId = "photogallery.photo",
                        root = new
                        {
                            type = "Column",
                            children = new object[]
                            {
                                new { type = "Text", text = "${photo.title}" },
                                new { type = "Text", text = "${photo.id}" },
                            },
                        },
                    },
                }),
                Document = JsonSerializer.Serialize(new
                {
                    dataModelUpdate = new
                    {
                        photo = new { id = currentEntity.Id, title = currentEntity.Extra, entityInfo },
                        state = new { screen = "detail", availableControls = new[] { "back" } },
                    },
                }),
            };
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Failure("The media library did not respond before the bounded provider timeout.");
        }
        catch (Exception exception)
        {
            return Failure($"The media library is unavailable: {exception.Message}");
        }
    }

    private IReadOnlyList<PhotoRecord> ReadSnapshot()
    {
        using var cancellation = new CancellationTokenSource(ProviderReadTimeout);
        return _library.ReadSnapshotAsync(cancellation.Token).GetAwaiter().GetResult();
    }

    private static TizenEntityPhoto ToEntity(PhotoRecord photo) => new()
    {
        Id = photo.Id,
        Extra = photo.Title,
        Location = photo.Location,
        Date = photo.CapturedAt.ToString("O"),
        Path = photo.Path,
        Note = photo.Note,
    };

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
