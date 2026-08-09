using PhotoGallery.Domain;

var photos = new[]
{
    PhotoRecord.Create("media-2", "Beach", new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero), "Busan", "/media/2", "holiday"),
    PhotoRecord.Create("media-1", "Garden", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), "Seoul", "/media/1", string.Empty)
};

var resolution = PhotoResolver.ResolveByIds(photos, ["media-2", "missing", "media-2", "media-1"]);
if (!resolution.Photos.Select(photo => photo.Id).SequenceEqual(["media-2", "media-2", "media-1"]) ||
    !resolution.UnresolvedIds.SequenceEqual(["missing"]))
{
    throw new InvalidOperationException("Photo resolver must preserve request order and duplicate IDs while reporting missing IDs.");
}

foreach (var invalid in new Action[]
{
    () => { PhotoRecord.Create("", "Photo", DateTimeOffset.UtcNow, null, null, null); },
    () => { PhotoSearchCriteria.Create(new string('x', PhotoSearchCriteria.MaximumKeywordLength + 1), null, null, 20); },
    () => { PhotoSearchCriteria.Create(null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 20); },
    () => { PhotoSearchCriteria.Create(null, null, null, PhotoSearchCriteria.MaximumResultCount + 1); },
    () => { PhotoResolver.ResolveByIds(photos, Enumerable.Repeat("media-1", PhotoResolver.MaximumIdsPerRequest + 1).ToArray()); }
})
{
    try
    {
        invalid();
        throw new InvalidOperationException("Photo boundary validation must reject invalid external input.");
    }
    catch (ArgumentException)
    {
    }
}

var galleryPhotos = new[]
{
    PhotoRecord.Create("media-3", "Lake", new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero), string.Empty, "/media/3", string.Empty),
    PhotoRecord.Create("media-4", "Forest", new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero), string.Empty, "/media/4", string.Empty)
};

var gallery = GalleryInteractionReducer.CreatePictures(galleryPhotos);
if (gallery.Screen != GalleryScreen.Pictures || gallery.SelectedPhotoId != "media-3" || gallery.FocusTarget != "pictures:media-3")
{
    throw new InvalidOperationException("Pictures must initially focus the first visible photo.");
}

gallery = GalleryInteractionReducer.OpenSearch(gallery);
gallery = GalleryInteractionReducer.SetQuery(gallery, "lake");
if (gallery.LoadState != GalleryLoadState.Loading || gallery.Query != "lake" || gallery.FocusTarget != GalleryInteractionReducer.SearchFocusTarget)
{
    throw new InvalidOperationException("Search input must enter a cancellable loading state while retaining search focus.");
}

gallery = GalleryInteractionReducer.ShowSearchResults(gallery, [galleryPhotos[0]]);
gallery = GalleryInteractionReducer.OpenDetail(gallery, "media-3");
gallery = GalleryInteractionReducer.RequestDelete(gallery);
if (!gallery.IsDeleteConfirmationOpen || gallery.FocusTarget != "delete-cancel")
{
    throw new InvalidOperationException("Delete must open a focus-trapped confirmation state.");
}

gallery = GalleryInteractionReducer.Back(gallery);
if (gallery.IsDeleteConfirmationOpen || gallery.FocusTarget != "detail-delete")
{
    throw new InvalidOperationException("Back from delete confirmation must restore Delete focus.");
}

gallery = GalleryInteractionReducer.RequestDelete(gallery);
gallery = GalleryInteractionReducer.DeleteFailed(gallery);
if (!gallery.IsDeleteConfirmationOpen || gallery.FocusTarget != "delete-confirm")
{
    throw new InvalidOperationException("A delete failure must keep the confirmation open with a deterministic recovery focus.");
}

gallery = GalleryInteractionReducer.DeleteSucceeded(gallery, [galleryPhotos[1]]);
if (gallery.Screen != GalleryScreen.Pictures || gallery.IsDeleteConfirmationOpen || gallery.SelectedPhotoId != "media-4" || gallery.FocusTarget != "pictures:media-4")
{
    throw new InvalidOperationException("Successful deletion must remove the modal and restore focus to a remaining Pictures card.");
}

gallery = GalleryInteractionReducer.OpenSearch(gallery);
gallery = GalleryInteractionReducer.ShowSearchResults(gallery, Array.Empty<PhotoRecord>());
if (gallery.LoadState != GalleryLoadState.SearchEmpty || gallery.FocusTarget != GalleryInteractionReducer.RetryFocusTarget)
{
    throw new InvalidOperationException("An empty search must expose a focusable recovery target.");
}

gallery = GalleryInteractionReducer.Back(gallery);
if (gallery.Screen != GalleryScreen.Pictures || gallery.LoadState != GalleryLoadState.Ready ||
    gallery.SelectedPhotoId != "media-4" || !gallery.VisiblePhotos.Select(photo => photo.Id).SequenceEqual(["media-4"]))
{
    throw new InvalidOperationException("Back from an empty search must restore the full Pictures snapshot and clear the query state.");
}

foreach (var invalidReducerTransition in new Action[]
{
    () => { GalleryInteractionReducer.OpenDetail(GalleryInteractionReducer.CreatePictures(galleryPhotos), "missing"); },
    () => { GalleryInteractionReducer.RequestDelete(GalleryInteractionReducer.CreatePictures(galleryPhotos)); },
    () => { GalleryInteractionReducer.CancelDelete(GalleryInteractionReducer.CreatePictures(galleryPhotos)); }
})
{
    try
    {
        invalidReducerTransition();
        throw new InvalidOperationException("The gallery reducer must reject invalid detail and destructive transitions.");
    }
    catch (ArgumentException)
    {
    }
    catch (InvalidOperationException)
    {
    }
}

Console.WriteLine("PhotoGallery.Domain.Tests PASS");
