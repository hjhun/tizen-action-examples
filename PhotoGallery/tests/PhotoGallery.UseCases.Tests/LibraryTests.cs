using PhotoGallery.Domain;
using PhotoGallery.UseCases;

internal static class LibraryTests
{
    public static async Task Run()
    {
        await DeleteConfirmationTests();
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
    private static async Task DeleteConfirmationTests()
    {
        var media = new FakeMedia();
        var service = new GalleryLibraryService(media);
        await service.RefreshAsync(CancellationToken.None);
        service.Show("a");
        var pending = new PhotoDeleteConfirmation(service.Current().Id);
        service.Show("b");
        Check(pending.TargetId == "a" && pending.Resolve(service.Snapshot).Id == "a", "selection cannot rebind confirmation");
        await service.DeleteAsync(pending.Resolve(service.Snapshot).Id, CancellationToken.None);
        Check(media.DeletedIds.SequenceEqual(["a"]) && service.Find("b").Id == "b", "confirmation deletes only A; B survives");

        media = new FakeMedia(); service = new GalleryLibraryService(media);
        await service.RefreshAsync(CancellationToken.None);
        pending = new PhotoDeleteConfirmation("a");
        service.Show("a");
        await service.DeleteAsync("a", CancellationToken.None);
        Check(service.Current().Id == "b", "external deletion advances viewer independently");
        try
        {
            await service.DeleteAsync(pending.Resolve(service.Snapshot).Id, CancellationToken.None);
            throw new Exception("Missing confirmation target must fail.");
        }
        catch (InvalidOperationException) { }
        Check(media.DeletedIds.SequenceEqual(["a"]) && service.Find("b").Id == "b", "missing A causes no additional deletion");

        media = new FakeMedia(); service = new GalleryLibraryService(media);
        await service.RefreshAsync(CancellationToken.None);
        pending = new PhotoDeleteConfirmation("a");
        media.Replace("a", "Updated title", true);
        await service.RefreshAsync(CancellationToken.None);
        Check(pending.TargetId == "a" && pending.Resolve(service.Snapshot).Title == "Updated title", "metadata refresh preserves target ID");
        media.Replace("a", "Device photo", false);
        await service.RefreshAsync(CancellationToken.None);
        Reject(() => pending.Resolve(service.Snapshot));
        Reject(() => new PhotoDeleteConfirmation("missing").Resolve(service.Snapshot));
        foreach (var id in new[] { "", " ", new string('x', PhotoRecord.MaximumIdLength + 1) })
            Reject(() => new PhotoDeleteConfirmation(id));
        Check(media.DeletedIds.Count == 0, "invalid and non-owned targets do not delete");
        PhotoDeleteConfirmation? session = pending;
        session = null; // Host models discarding the state; native CloseModal wiring is reviewed separately.
        session = new PhotoDeleteConfirmation("b");
        Check(session.Resolve(service.Snapshot).Id == "b" && pending.TargetId == "a", "new confirmation is independent of the old immutable target");
        Console.WriteLine("PhotoDeleteConfirmation tests PASS (stable target, missing, metadata, ownership, independent sessions)");
    }
    static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Expected bounded failure"); }
    sealed class FakeMedia : IMutablePhotoLibrary
    {
        public List<string> DeletedIds { get; } = [];
        public void Replace(string id, string title, bool owned) => _items = _items.Select(x => x.Id == id ? x with { Title = title, Owned = owned } : x).ToList();
        public bool FailMutation;
        public bool FailAfterMutation;
        private List<PhotoRecord> _items = [
            PhotoRecord.Create("a","Lake",DateTimeOffset.Parse("2026-09-06T10:00:00Z"),"","/images/a.png","") with { Owned = true },
            PhotoRecord.Create("b","Garden",DateTimeOffset.Parse("2026-09-05T10:00:00Z"),"","/images/b.png","") with { Owned = true }];
        public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<PhotoRecord>>(_items.ToArray());
        public Task ImportAsync(string path,string title,CancellationToken token) => Task.CompletedTask;
        public Task DeleteAsync(string id,CancellationToken token) { DeletedIds.Add(id); _items.RemoveAll(x=>x.Id==id); return Task.CompletedTask; }
        public Task SetFavoriteAsync(string id,bool value,CancellationToken token)
        { if(FailMutation) throw new IOException("Disk full"); _items=_items.Select(x=>x.Id==id?x with { Favorite=value }:x).ToList();if(FailAfterMutation)throw new IOException("Acknowledgment lost");return Task.CompletedTask; }
    }
}
