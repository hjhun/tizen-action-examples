using Calendar.Domain;
using Calendar.Persistence;
using Calendar.UseCases;
using PhotoGallery.Domain;
using PhotoGallery.UseCases;
using Photo = RPCPort.PhotoGalleryActionProvider;
using Event = RPCPort.CalendarActionProvider;

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

var library = new CountingLibrary();
var gallery = new GalleryLibraryService(library);
await gallery.RefreshAsync(CancellationToken.None);
var photoChanges = 0;
gallery.Changed += () => photoChanges++;
var photo = new PhotoGallery.ActionProvider.PhotoGalleryService(gallery);
foreach (var query in new[]
{
    new Photo.TizenEntityPhotoQuery { DateFrom = "" },
    new Photo.TizenEntityPhotoQuery { DateTo = "" },
    new Photo.TizenEntityPhotoQuery { Location = "" },
    new Photo.TizenEntityPhotoQuery { Person = "" },
    new Photo.TizenEntityPhotoQuery { Activity = "" },
    new Photo.TizenEntityPhotoQuery { Object = "" },
})
{
    var status = photo.Search(query, out var result);
    Assert(!status.Success && status.Reason.StartsWith("unavailable:") && result.Count == 0,
        "Present unsupported Photo filters must fail with empty results.");
}
Assert(!photo.AddPhoto(new(), out var added).Success && added is null,
    "Unavailable AddPhoto must omit optional result.");
Assert(!photo.DeletePhoto([new() { Id = "owned" }]).Success,
    "Unavailable DeletePhoto must fail before storage effects.");
Assert(!photo.StartSlideshow([new() { Id = "owned" }], out var slideshow).Success && slideshow.Count == 0,
    "Unavailable StartSlideshow must initialize the required list.");
Assert(!photo.GetMemories(out var memories).Success && memories.Memories.Count == 0
    && memories.RecommendationIndex == -1 && memories.Locked == false,
    "Unavailable GetMemories must initialize required fields.");
Assert(!photo.PlayMemory(new()).Success, "Unavailable PlayMemory must fail.");
Assert(library.Reads == 1 && library.Mutations == 0 && photoChanges == 0
    && gallery.Ready && !gallery.Slideshow && gallery.Snapshot.Count == 0,
    "Rejected Photo boundaries must preserve service state and avoid library work.");

var now = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
var item = CalendarEvent.Create("existing", "Original", now, now.AddHours(1), "", "");
var events = new CalendarEventRepository([item]);
var reminders = new CalendarReminderRepository([]);
var persistence = new CountingPersistence();
var alarms = new CountingAlarms();
var commands = new CalendarCommandService(events, reminders, persistence, alarms);
var changes = 0;
commands.Changed += () => changes++;
var calendar = new Calendar.ActionProvider.CalendarService(events, commands);
var version = events.Version;
foreach (var query in new[]
{
    new Event.TizenEntityCalendarQuery { CalendarId = "" },
    new Event.TizenEntityCalendarQuery { Profiles = [] },
})
{
    var status = calendar.Search(query, out var result);
    Assert(!status.Success && status.Reason.StartsWith("unavailable:") && result.Count == 0,
        "Present unsupported Calendar filters must fail with empty results.");
}
Assert(!calendar.AddEvent(new() { Id = "new", Title = "New" }, out var addedEvent).Success
    && addedEvent is null, "Unavailable AddEvent must omit optional result.");
Assert(!calendar.UpdateEvent(new() { Id = "existing" }, new() { Title = "Changed" }, out var updated).Success
    && updated is not null && updated.Title is not null,
    "Unavailable UpdateEvent must initialize its required result.");
Assert(!calendar.DeleteEvent(new() { Id = "existing", StartDate = "" }).Success
    && !calendar.DeleteEvent(new() { Id = "existing", Recurrence = new() }).Success,
    "Present occurrence/recurrence selectors must reject before deletion.");
Assert(events.Version == version && events.Snapshot().SequenceEqual(new[] { item })
    && reminders.Snapshot().Count == 0 && persistence.Calls == 0 && alarms.Calls == 0 && changes == 0,
    "Rejected Calendar boundaries must preserve repositories and avoid persistence/alarm effects.");
Console.WriteLine("PASS: Photo/Calendar unavailable outputs, filter presence, and no effects (no native Parcel claim).");

sealed class CountingLibrary : IMutablePhotoLibrary
{
    public int Reads { get; private set; }
    public int Mutations { get; private set; }
    public Task<IReadOnlyList<PhotoRecord>> ReadSnapshotAsync(CancellationToken token)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<PhotoRecord>>([]);
    }
    public Task ImportAsync(string path, string title, CancellationToken token) { Mutations++; return Task.CompletedTask; }
    public Task DeleteAsync(string id, CancellationToken token) { Mutations++; return Task.CompletedTask; }
    public Task SetFavoriteAsync(string id, bool favorite, CancellationToken token) { Mutations++; return Task.CompletedTask; }
}

sealed class CountingPersistence : ICalendarPersistence
{
    public int Calls { get; private set; }
    public CalendarStoreDocument Load() { Calls++; return new(1, [], []); }
    public void Save(CalendarStoreDocument document) { Calls++; }
}

sealed class CountingAlarms : IReminderAlarmScheduler
{
    public int Calls { get; private set; }
    public int? Schedule(CalendarReminder reminder) { Calls++; return 1; }
    public void Cancel(int alarmId) { Calls++; }
}
