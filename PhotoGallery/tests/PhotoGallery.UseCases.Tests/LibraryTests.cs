using PhotoGallery.Domain;
using PhotoGallery.UseCases;

internal static class LibraryTests
{
    public static async Task Run()
    {
        var photos = new FakeMedia();
        var service = new GalleryLibraryService(photos);
        await service.RefreshAsync(CancellationToken.None);
        Check(service.Search("b", "", "Photo", 1).Single().Id == "b", "ID filter precedes limit");
        Reject(() => service.Search("", "", "Favorites", 20));
        Reject(() => service.Search("", new string('x', 257), "", 20));
        var resolution = service.Resolve(["b", "missing", "b"]);
        Check(resolution.Photos.Select(x => x.Id).SequenceEqual(["b", "b"]), "resolver preserves duplicates/order");
        Check(resolution.UnresolvedIds.SequenceEqual(["missing"]), "unresolved IDs");
        Reject(() => service.Show("missing"));
        Reject(() => service.Current());
        service.Show("b"); Check(service.Current().Id == "b", "current selected photo");
        service.StartSlideshow(); Reject(service.StartSlideshow); service.Advance(1); Check(service.Current().Id == "a", "slideshow wraps");
        service.StopSlideshow(); Reject(service.StopSlideshow);
        await service.SetFavoriteAsync("b", true, CancellationToken.None);
        Check(service.Resolve(["b"]).Photos[0].Favorite, "favorite mutation refreshes snapshot");
        photos.FailMutation = true;
        try { await service.SetFavoriteAsync("b", false, CancellationToken.None); throw new Exception("Expected failure"); }
        catch (IOException) { }
        Check(service.Resolve(["b"]).Photos[0].Favorite, "failed persistence keeps published state");
        photos.FailMutation = false;
        photos.FailAfterMutation = true;
        try { await service.SetFavoriteAsync("b", false, CancellationToken.None); throw new Exception("Expected partial failure"); }
        catch (IOException) { }
        Check(!service.Resolve(["b"]).Photos[0].Favorite, "after a partial platform failure queries reflect durable state");
        photos.FailAfterMutation = false;
        await service.DeleteAsync("b", CancellationToken.None);
        Check(service.Search("b", "", "", 20).Count == 0, "delete query postcondition");
        Check(service.Current().Id == "a", "delete advances current photo");
        service.CloseViewer(); Reject(() => service.Current());
        var presentation = PhotoPresentation.Create(photos.ReadSnapshotAsync(CancellationToken.None).Result[0] with { Path = "/private/secret.png", Location = "private GPS", Note = "private note" });
        using var template = System.Text.Json.JsonDocument.Parse(presentation.Template);
        var surfaceId = template.RootElement.GetProperty("surfaceUpdate").GetProperty("surfaceId").GetString()!;
        Check(surfaceId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':'), "DisplayPresentation supported identifier profile");
        Check(!presentation.Document.Contains("private") && presentation.Template.Contains("surfaceUpdate"), "privacy-bounded legacy presentation");
        Console.WriteLine("LibraryTests PASS (ID query, resolver, viewer/slideshow, mutation publication)");
    }
    static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Expected bounded failure"); }
    sealed class FakeMedia : IMutablePhotoLibrary
    {
        public bool FailMutation;
        public bool FailAfterMutation;
        private List<PhotoRecord> _items = [
            PhotoRecord.Create("a","Lake",DateTimeOffset.Parse("2026-09-06T10:00:00Z"),"","/images/a.png","") with { Owned = true },
            PhotoRecord.Create("b","Garden",DateTimeOffset.Parse("2026-09-05T10:00:00Z"),"","/images/b.png","") with { Owned = true }];
        public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<PhotoRecord>>(_items.ToArray());
        public Task ImportAsync(string path,string title,CancellationToken token) => Task.CompletedTask;
        public Task DeleteAsync(string id,CancellationToken token) { _items.RemoveAll(x=>x.Id==id); return Task.CompletedTask; }
        public Task SetFavoriteAsync(string id,bool value,CancellationToken token)
        { if(FailMutation) throw new IOException("Disk full"); _items=_items.Select(x=>x.Id==id?x with { Favorite=value }:x).ToList();if(FailAfterMutation)throw new IOException("Acknowledgment lost");return Task.CompletedTask; }
    }
}
