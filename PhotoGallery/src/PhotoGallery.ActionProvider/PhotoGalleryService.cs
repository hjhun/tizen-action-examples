#nullable enable

using System.Diagnostics;
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

    public override TizenEntityStatus AddImage(TizenEntityPhoto photo)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlation = SafeCorrelate(photo?.Id);
        var isValid = HasValidPhotoId(photo);
        var status = isValid
            ? Failure("Adding media is unavailable until the target MediaContent registration capability is verified.")
            : Failure("A stable photo ID is required.");
        LogInvocation(
            "Tv_Tizen.Action.Photo_AddImage",
            correlation,
            isValid ? "valid" : "invalid",
            isValid ? "capability-unavailable" : "validation",
            status,
            stopwatch);
        return status;
    }

    public override TizenEntityStatus DeleteImage(TizenEntityPhoto photo)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlation = SafeCorrelate(photo?.Id);
        var isValid = HasValidPhotoId(photo);
        var status = isValid
            ? Failure("Deleting media is unavailable until the target MediaContent mutation capability is verified.")
            : Failure("A stable photo ID is required.");
        LogInvocation(
            "Tv_Tizen.Action.Photo_DeleteImage",
            correlation,
            isValid ? "valid" : "invalid",
            isValid ? "capability-unavailable" : "validation",
            status,
            stopwatch);
        return status;
    }

    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityPhoto> result)
    {
        var stopwatch = Stopwatch.StartNew();
        result = [];
        if (query is null || query.Keyword?.Length > PhotoSearchCriteria.MaximumKeywordLength ||
            query.Number > PhotoSearchCriteria.MaximumResultCount || query.Number < 0 ||
            !string.IsNullOrEmpty(query.Category))
        {
            var status = Failure("Search requires a bounded keyword, a result count from 0 to 200, and no category filter.");
            LogInvocation("Tv_Tizen.Action.Photo_Search", "none", "invalid", "validation", status, stopwatch);
            return status;
        }

        try
        {
            using var cancellation = new CancellationTokenSource(ProviderReadTimeout);
            var criteria = PhotoSearchCriteria.Create(query.Keyword, null, null, query.Number == 0 ? 20 : query.Number);
            result = _queries.SearchAsync(criteria, cancellation.Token).GetAwaiter().GetResult().Photos.Select(ToEntity).ToList();
            var status = Success();
            LogInvocation("Tv_Tizen.Action.Photo_Search", "none", "valid", "completed", status, stopwatch);
            return status;
        }
        catch (OperationCanceledException exception)
        {
            var status = Failure("The media library did not respond before the bounded provider timeout.");
            LogInvocation("Tv_Tizen.Action.Photo_Search", "none", "valid", "timeout", status, stopwatch, exception);
            return status;
        }
        catch (Exception exception)
        {
            var status = Failure("The media library is unavailable.");
            LogInvocation("Tv_Tizen.Action.Photo_Search", "none", "valid", "exception", status, stopwatch, exception);
            return status;
        }
    }

    public override TizenEntityStatus GetPhotoByIds(
        List<string> ids,
        out List<TizenEntityPhoto> result,
        out List<string> unresolvedIds)
    {
        var stopwatch = Stopwatch.StartNew();
        result = [];
        unresolvedIds = [];
        if (ids is null || ids.Count > PhotoResolver.MaximumIdsPerRequest ||
            ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > PhotoRecord.MaximumIdLength))
        {
            var status = Failure("ids must contain at most 100 non-empty stable IDs, each no longer than 256 characters.");
            LogInvocation("Tv_Tizen.Action.Photo_GetPhotoByIds", SafeCorrelate(ids), "invalid", "validation", status, stopwatch);
            return status;
        }

        try
        {
            var resolution = PhotoResolver.ResolveByIds(ReadSnapshot(), ids);
            result = resolution.Photos.Select(ToEntity).ToList();
            unresolvedIds = resolution.UnresolvedIds.ToList();
            var status = Success();
            LogInvocation("Tv_Tizen.Action.Photo_GetPhotoByIds", SafeCorrelate(ids), "valid", "completed", status, stopwatch);
            return status;
        }
        catch (OperationCanceledException exception)
        {
            var status = Failure("The media library did not respond before the bounded provider timeout.");
            LogInvocation("Tv_Tizen.Action.Photo_GetPhotoByIds", SafeCorrelate(ids), "valid", "timeout", status, stopwatch, exception);
            return status;
        }
        catch (Exception exception)
        {
            var status = Failure("The media library is unavailable.");
            LogInvocation("Tv_Tizen.Action.Photo_GetPhotoByIds", SafeCorrelate(ids), "valid", "exception", status, stopwatch, exception);
            return status;
        }
    }

    public override TizenEntityStatus ToPresentation(TizenEntityPhoto photo, out TizenEntityPresentation result)
    {
        var stopwatch = Stopwatch.StartNew();
        result = new TizenEntityPresentation { Template = string.Empty, Document = string.Empty };
        if (photo is null || string.IsNullOrWhiteSpace(photo.Id) || photo.Id.Length > PhotoRecord.MaximumIdLength)
        {
            var status = Failure("A stable photo ID is required.");
            LogInvocation("Tv_Tizen.Action.Photo_ToPresentation", SafeCorrelate(photo?.Id), "invalid", "validation", status, stopwatch);
            return status;
        }

        try
        {
            var resolved = PhotoResolver.ResolveByIds(ReadSnapshot(), [photo.Id]);
            if (resolved.Photos.Count == 0)
            {
                var status = Failure("The requested photo is no longer available.");
                LogInvocation("Tv_Tizen.Action.Photo_ToPresentation", SafeCorrelate(photo.Id), "valid", "not-found", status, stopwatch);
                return status;
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
            var completed = Success();
            LogInvocation("Tv_Tizen.Action.Photo_ToPresentation", SafeCorrelate(photo.Id), "valid", "completed", completed, stopwatch);
            return completed;
        }
        catch (OperationCanceledException exception)
        {
            var status = Failure("The media library did not respond before the bounded provider timeout.");
            LogInvocation("Tv_Tizen.Action.Photo_ToPresentation", SafeCorrelate(photo?.Id), "valid", "timeout", status, stopwatch, exception);
            return status;
        }
        catch (Exception exception)
        {
            var status = Failure("The media library is unavailable.");
            LogInvocation("Tv_Tizen.Action.Photo_ToPresentation", SafeCorrelate(photo?.Id), "valid", "exception", status, stopwatch, exception);
            return status;
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

    private static bool HasValidPhotoId(TizenEntityPhoto? photo) =>
        photo is not null && !string.IsNullOrWhiteSpace(photo.Id) && photo.Id.Length <= PhotoRecord.MaximumIdLength;

    private static string SafeCorrelate(string? stableId)
    {
        try
        {
            return PhotoActionDiagnostics.Correlate(stableId);
        }
        catch
        {
            return "unavailable";
        }
    }

    private static string SafeCorrelate(IReadOnlyCollection<string>? stableIds)
    {
        try
        {
            return PhotoActionDiagnostics.Correlate(stableIds);
        }
        catch
        {
            return "unavailable";
        }
    }

    private static void LogInvocation(
        string action,
        string correlation,
        string validation,
        string branch,
        TizenEntityStatus status,
        Stopwatch stopwatch,
        Exception? exception = null)
    {
        try
        {
            var message = PhotoActionDiagnostics.Format(
                action,
                correlation,
                validation,
                branch,
                status.Success ? "success" : "failure",
                stopwatch,
                exception);

            if (branch == "exception")
            {
                Tizen.Log.Error("PhotoGallery", message);
            }
            else if (branch == "timeout")
            {
                Tizen.Log.Warn("PhotoGallery", message);
            }
            else
            {
                Tizen.Log.Info("PhotoGallery", message);
            }
        }
        catch (Exception diagnosticException)
        {
            try
            {
                Tizen.Log.Error(
                    "PhotoGallery",
                    $"action={action} correlation={correlation} validation=not-run " +
                    $"branch=diagnostic-failure status={(status.Success ? "success" : "failure")} " +
                    $"durationMs={Math.Max(0, stopwatch.ElapsedMilliseconds)} " +
                    $"exception={diagnosticException.GetType().Name}");
            }
            catch
            {
                // Diagnostics must never alter an Action's typed result.
            }
        }
    }
}
