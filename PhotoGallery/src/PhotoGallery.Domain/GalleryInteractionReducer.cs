namespace PhotoGallery.Domain;

/// <summary>
/// Pure interaction state for the Pictures, Search, Detail, and destructive-confirmation
/// flow. NUI adapters map focus targets to real actors and keep media I/O outside this reducer.
/// </summary>
public enum GalleryScreen
{
    Pictures,
    Search,
    Detail
}

public enum GalleryLoadState
{
    Ready,
    Loading,
    Empty,
    SearchEmpty,
    Unavailable
}

public sealed record GalleryState(
    GalleryScreen Screen,
    GalleryLoadState LoadState,
    IReadOnlyList<PhotoRecord> LibraryPhotos,
    IReadOnlyList<PhotoRecord> VisiblePhotos,
    string Query,
    string? SelectedPhotoId,
    string? PendingDeletePhotoId,
    string FocusTarget)
{
    public bool IsDeleteConfirmationOpen => PendingDeletePhotoId is not null;
}

public static class GalleryInteractionReducer
{
    public const string SearchFocusTarget = "search";
    public const string RetryFocusTarget = "retry";

    public static GalleryState CreatePictures(IReadOnlyList<PhotoRecord> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        var selectedId = photos.FirstOrDefault()?.Id;
        return new GalleryState(
            GalleryScreen.Pictures,
            photos.Count == 0 ? GalleryLoadState.Empty : GalleryLoadState.Ready,
            photos.ToArray(),
            photos.ToArray(),
            string.Empty,
            selectedId,
            null,
            selectedId is null ? RetryFocusTarget : PictureFocusTarget(selectedId));
    }

    public static GalleryState OpenSearch(GalleryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with
        {
            Screen = GalleryScreen.Search,
            LoadState = GalleryLoadState.Ready,
            Query = string.Empty,
            PendingDeletePhotoId = null,
            FocusTarget = SearchFocusTarget
        };
    }

    public static GalleryState SetQuery(GalleryState state, string? query)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Screen != GalleryScreen.Search)
        {
            throw new InvalidOperationException("A query can only be changed from Search.");
        }

        var criteria = PhotoSearchCriteria.Create(query, null, null, 1);
        return state with { Query = criteria.Keyword, LoadState = GalleryLoadState.Loading };
    }

    public static GalleryState ShowSearchResults(GalleryState state, IReadOnlyList<PhotoRecord> photos)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(photos);
        if (state.Screen != GalleryScreen.Search)
        {
            throw new InvalidOperationException("Search results can only be shown in Search.");
        }

        var selectedId = photos.FirstOrDefault()?.Id;
        return state with
        {
            LoadState = photos.Count == 0 ? GalleryLoadState.SearchEmpty : GalleryLoadState.Ready,
            VisiblePhotos = photos.ToArray(),
            SelectedPhotoId = selectedId,
            FocusTarget = selectedId is null ? RetryFocusTarget : PictureFocusTarget(selectedId)
        };
    }

    public static GalleryState OpenDetail(GalleryState state, string photoId)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureVisiblePhoto(state, photoId);
        return state with
        {
            Screen = GalleryScreen.Detail,
            SelectedPhotoId = photoId,
            PendingDeletePhotoId = null,
            FocusTarget = "detail-back"
        };
    }

    public static GalleryState RequestDelete(GalleryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Screen != GalleryScreen.Detail || state.SelectedPhotoId is null || state.LoadState != GalleryLoadState.Ready)
        {
            throw new InvalidOperationException("Only a ready detail photo can be deleted.");
        }

        return state with
        {
            PendingDeletePhotoId = state.SelectedPhotoId,
            FocusTarget = "delete-cancel"
        };
    }

    public static GalleryState CancelDelete(GalleryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsDeleteConfirmationOpen)
        {
            throw new InvalidOperationException("There is no delete confirmation to cancel.");
        }

        return state with { PendingDeletePhotoId = null, FocusTarget = "detail-delete" };
    }

    public static GalleryState DeleteFailed(GalleryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsDeleteConfirmationOpen)
        {
            throw new InvalidOperationException("A delete failure requires an open confirmation.");
        }

        return state with { FocusTarget = "delete-confirm" };
    }

    public static GalleryState DeleteSucceeded(GalleryState state, IReadOnlyList<PhotoRecord> remainingPhotos)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(remainingPhotos);
        if (!state.IsDeleteConfirmationOpen)
        {
            throw new InvalidOperationException("A delete success requires an open confirmation.");
        }

        return CreatePictures(remainingPhotos);
    }

    public static GalleryState Back(GalleryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsDeleteConfirmationOpen)
        {
            return CancelDelete(state);
        }

        return state.Screen switch
        {
            GalleryScreen.Detail when state.SelectedPhotoId is not null => state with
            {
                Screen = GalleryScreen.Pictures,
                FocusTarget = PictureFocusTarget(state.SelectedPhotoId)
            },
            GalleryScreen.Search => state with
            {
                Screen = GalleryScreen.Pictures,
                Query = string.Empty,
                LoadState = state.LibraryPhotos.Count == 0 ? GalleryLoadState.Empty : GalleryLoadState.Ready,
                VisiblePhotos = state.LibraryPhotos,
                SelectedPhotoId = state.SelectedPhotoId is not null && state.LibraryPhotos.Any(photo => photo.Id == state.SelectedPhotoId)
                    ? state.SelectedPhotoId
                    : state.LibraryPhotos.FirstOrDefault()?.Id,
                FocusTarget = state.LibraryPhotos.FirstOrDefault(photo => photo.Id == state.SelectedPhotoId) is not null
                    ? PictureFocusTarget(state.SelectedPhotoId!)
                    : state.LibraryPhotos.FirstOrDefault() is { } photo ? PictureFocusTarget(photo.Id) : RetryFocusTarget
            },
            _ => state
        };
    }

    public static string PictureFocusTarget(string photoId) => $"pictures:{photoId}";

    private static void EnsureVisiblePhoto(GalleryState state, string photoId)
    {
        if (string.IsNullOrWhiteSpace(photoId) ||
            !state.VisiblePhotos.Any(photo => string.Equals(photo.Id, photoId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The requested photo is not visible in the current gallery state.", nameof(photoId));
        }
    }
}
