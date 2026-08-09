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

Console.WriteLine("PhotoGallery.Domain.Tests PASS");
