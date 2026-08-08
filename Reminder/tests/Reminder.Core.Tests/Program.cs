using Reminder.App;
using Reminder.Domain;
using Reminder.Persistence;
using Reminder.UseCases;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var now = new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
var store = new MemoryScheduleStore();
var resources = new DeterministicReservationSimulator();
var service = new ScheduleService(store, resources, () => now);

var fullHdViewport = ProportionalViewport.Create(1920, 1080);
Assert(fullHdViewport == new ProportionalViewport(1.0f, 0.0f, 0.0f, 1920.0f, 1080.0f), "Full HD must preserve the reference canvas exactly.");
var hdViewport = ProportionalViewport.Create(1280, 720);
Assert(Math.Abs(hdViewport.Scale - (2.0f / 3.0f)) < 0.0001f && hdViewport.OffsetX == 0 && hdViewport.OffsetY == 0, "1280x720 must uniformly scale the 16:9 canvas without offsets.");
var fourByThreeViewport = ProportionalViewport.Create(1440, 1080);
Assert(fourByThreeViewport.Scale == 0.75f && fourByThreeViewport.OffsetX == 0 && fourByThreeViewport.OffsetY == 135.0f, "4:3 windows must vertically center a proportional canvas.");
var ultraWideViewport = ProportionalViewport.Create(2560, 1080);
Assert(ultraWideViewport.Scale == 1.0f && ultraWideViewport.OffsetX == 320.0f && ultraWideViewport.OffsetY == 0, "Ultrawide windows must horizontally center a proportional canvas.");
var insetViewport = ProportionalViewport.Create(1920, 1080, 20, 30, 40, 50);
Assert(
    Math.Abs(insetViewport.Scale - (1000.0f / 1080.0f)) < 0.0001f &&
    Math.Abs(insetViewport.OffsetX - 61.1111f) < 0.001f &&
    Math.Abs(insetViewport.OffsetY - 30.0f) < 0.001f,
    "Platform insets must constrain and center the proportional canvas inside the available area.");
Assert(!ProportionalViewport.TryCreate(0, 1080, out _), "Invalid window dimensions must not create a viewport.");

var milk = ReminderItem.Create("rem-1", "  Buy milk  ", now.AddHours(2), "  oat  ");
Assert(service.CreateReminder(milk).Success, "Create must succeed.");
Assert(service.CreateReminder(milk).Success && service.Snapshot.Reminders.Count == 1, "Same create must be idempotent.");
Assert(service.CreateReminder(milk with { Title = "Different" }).Code == ResultCode.Conflict, "Different duplicate ID must conflict.");
Assert(service.CreateReminder(ReminderItem.Create("rem-2", "No alert", null, "")).Success, "No-alert reminders are supported.");
Assert(service.CreateReminder(ReminderItem.Create("rem-3", "Late", now.AddMinutes(-1), "")).Code == ResultCode.Invalid, "Past due creation must fail.");
Assert(service.SearchReminders(new ReminderQuery("", ReminderCategory.NoAlert, 50)).Single().Id == "rem-2", "No-alert filtering must exclude dated reminders.");

var today = service.SearchReminders(new ReminderQuery("", ReminderCategory.Today, 50));
Assert(today.Count == 1 && today[0].Id == "rem-1", "Today filtering must use local date and deterministic due order.");
Assert(service.SearchReminders(new ReminderQuery("OAT", ReminderCategory.All, 50)).Single().Id == "rem-1", "Search must match note case-insensitively.");
Assert(service.CompleteReminder("rem-1").Success, "Complete must succeed.");
Assert(service.CompleteReminder("rem-1").Success, "Complete must be idempotent.");
Assert(service.SearchReminders(new ReminderQuery("", ReminderCategory.Completed, 50)).Single().Id == "rem-1", "Completed filter must expose completed reminders.");
Assert(service.UpdateReminder(milk with { Completed = false }).Code == ResultCode.Conflict, "Update cannot implicitly reopen a completed reminder.");
Assert(service.DeleteReminder("missing").Success, "Delete must be idempotent for absent IDs.");

var viewing = ReservationItem.Create("res-v", ReservationKind.Viewing, "7", "News", now.AddHours(1), now.AddHours(2), ReservationRepeat.Once);
var recording = ReservationItem.Create("res-r", ReservationKind.Recording, "9", "Film", now.AddHours(3), now.AddHours(5), ReservationRepeat.Weekly);
Assert(service.AddReservation(viewing, ReservationKind.Viewing).Success, "Viewing reservation must be added.");
Assert(service.AddReservation(recording, ReservationKind.Recording).Success, "Recording reservation must be added.");
Assert(service.GetReservations().Select(x => x.Id).SequenceEqual(["res-v", "res-r"]), "Reservations must sort by start then ID.");
Assert(service.CancelReservation("res-v", ReservationKind.Recording).Code == ResultCode.Conflict, "Wrong-kind cancellation must fail.");
Assert(service.CancelReservation("res-v", ReservationKind.Viewing).Success, "Matching cancellation must succeed.");
Assert(resources.CreatedHandles.Where(x => x.StartsWith("reservation:", StringComparison.Ordinal)).SequenceEqual(new[] { "reservation:res-v:viewing", "reservation:res-r:recording" }), "Common simulator reservation handles must be deterministic.");

var failingStore = new MemoryScheduleStore { FailSaves = true };
var compensatingResources = new DeterministicReservationSimulator();
var failingService = new ScheduleService(failingStore, compensatingResources, () => now);
var failure = failingService.AddReservation(viewing, ReservationKind.Viewing);
Assert(failure.Code == ResultCode.Internal && compensatingResources.CancelledHandles.SequenceEqual(new[] { "reservation:res-v:viewing" }), "Failed persistence must compensate only the newly created handle.");
Assert(failingService.Snapshot.Reservations.Count == 0, "Failed persistence must not publish state.");

var restartStore = new MemoryScheduleStore();
restartStore.Save(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion,
    [ReminderItem.Create("restart-rem", "Restart", now.AddHours(5), "") with { ResourceHandle = "stale-reminder" }],
    [viewing with { ResourceHandle = "stale-reservation" }]));
var restartResources = new DeterministicReservationSimulator();
var restarted = new ScheduleService(restartStore, restartResources, () => now);
Assert(restarted.Snapshot.Reminders.Single().ResourceHandle == "reminder:restart-rem", "Restart must reconcile a future reminder handle.");
Assert(restarted.Snapshot.Reservations.Single().ResourceHandle == "reservation:res-v:viewing", "Restart must reconcile a future reservation handle.");
Assert(restartResources.CancelledHandles.SequenceEqual(new[] { "stale-reminder", "stale-reservation" }), "Restart must cancel only persisted app-owned stale handles.");

var temp = Path.Combine(Path.GetTempPath(), $"reminder-{Guid.NewGuid():N}.json");
try
{
    var jsonStore = new JsonScheduleStore(temp);
    jsonStore.Save(service.Snapshot);
    var restored = jsonStore.Load();
    Assert(restored.Reminders.Count == 2 && restored.Reservations.Count == 1, "JSON persistence must round-trip canonical state.");
    File.WriteAllText(temp, "not-json");
    var corrupt = false;
    try { jsonStore.Load(); } catch (ScheduleStoreCorruptException) { corrupt = true; }
    Assert(corrupt && Directory.GetFiles(Path.GetDirectoryName(temp)!, Path.GetFileName(temp) + ".corrupt*").Length == 1, "Corrupt stores must be backed up and reported.");
}
finally
{
    foreach (var file in Directory.GetFiles(Path.GetDirectoryName(temp)!, Path.GetFileName(temp) + "*")) File.Delete(file);
}

Console.WriteLine("Reminder.Core.Tests: PASS (30 assertions)");
