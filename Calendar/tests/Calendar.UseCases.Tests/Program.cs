using Calendar.Domain;
using Calendar.Persistence;
using Calendar.UseCases;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var events = new CalendarEventRepository([]);
var reminders = new CalendarReminderRepository([]);
var persistence = new RecordingPersistence();
var alarms = new RecordingAlarmScheduler();
var service = new CalendarCommandService(events, reminders, persistence, alarms);
var calendarEvent = CalendarEvent.Create(
    "event-review",
    "Design review",
    new DateTimeOffset(2026, 8, 10, 15, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 8, 10, 16, 0, 0, TimeSpan.Zero),
    "Candidate B",
    "Studio");

var result = service.CreateEvent(calendarEvent, [10, 60]);
Assert(result.Success, "Creating a valid event must succeed.");
Assert(events.ResolveByIds([calendarEvent.Id]).Events.Count == 1, "A successful create must publish the event to the shared repository.");
Assert(reminders.FindByCalendarEventId(calendarEvent.Id).Select(item => item.OffsetMinutes).SequenceEqual([60, 10]), "A successful create must publish both event-linked reminders in due-date order.");
Assert(alarms.Scheduled.Count == 2 && persistence.Saved.Count == 1, "A successful create must schedule every linked reminder and persist exactly once.");

var oldEvent = CalendarEvent.Create(
    "event-planning",
    "Planning",
    new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 8, 12, 11, 0, 0, TimeSpan.Zero),
    string.Empty,
    "Studio");
var oldReminder = CalendarReminder.CreateForEvent(
    "reminder:event-planning:30",
    oldEvent.Title,
    oldEvent.Start,
    oldEvent.Id,
    30,
    oldEvent.Note) with { AlarmId = 77 };
var updateEvents = new CalendarEventRepository([oldEvent]);
var updateReminders = new CalendarReminderRepository([oldReminder]);
var updatePersistence = new RecordingPersistence();
var updateAlarms = new RecordingAlarmScheduler();
var updateService = new CalendarCommandService(updateEvents, updateReminders, updatePersistence, updateAlarms);
var updatedEvent = oldEvent with
{
    Title = "Planning final",
    Start = oldEvent.Start.AddHours(1),
    End = oldEvent.End.AddHours(1),
};
result = updateService.UpdateEvent(updatedEvent, [10]);
Assert(result.Success, "Updating an existing event must succeed.");
Assert(updateEvents.ResolveByIds([oldEvent.Id]).Events.Single().Title == "Planning final", "Update must publish the replacement event.");
Assert(updateReminders.FindByCalendarEventId(oldEvent.Id).Select(item => item.OffsetMinutes).SequenceEqual([10]), "Update must replace linked reminder offsets.");
Assert(updateAlarms.Cancelled.SequenceEqual([77]) && updateAlarms.Scheduled.Count == 1, "Update must schedule replacements and cancel old alarm handles.");

result = updateService.DeleteEvent(oldEvent.Id);
Assert(result.Success, "Deleting an existing event must succeed.");
Assert(updateEvents.ResolveByIds([oldEvent.Id]).UnresolvedIds.Count == 1, "Delete must remove the event from the shared repository.");
Assert(updateReminders.FindByCalendarEventId(oldEvent.Id).Count == 0, "Delete must remove every linked reminder.");
Assert(updateAlarms.Cancelled.SequenceEqual([77, 100]), "Delete must cancel the currently persisted linked alarm handle.");

var reminderEvents = new CalendarEventRepository([]);
var standaloneReminders = new CalendarReminderRepository([]);
var reminderPersistence = new RecordingPersistence();
var reminderAlarms = new RecordingAlarmScheduler();
var reminderService = new CalendarCommandService(reminderEvents, standaloneReminders, reminderPersistence, reminderAlarms);
var standaloneReminder = CalendarReminder.Create(
    "reminder-buy-milk",
    "Buy milk",
    new DateTimeOffset(2026, 8, 10, 18, 0, 0, TimeSpan.Zero),
    "On the way home");
result = reminderService.CreateReminder(standaloneReminder);
Assert(result.Success && standaloneReminders.Find(standaloneReminder.Id) is not null, "Creating an independent reminder must publish it to the shared reminder repository.");
Assert(reminderAlarms.Scheduled.Count == 1 && reminderPersistence.Saved.Count == 1, "Creating an independent reminder must schedule and persist it.");
var updatedReminder = standaloneReminder with
{
    Title = "Buy milk and bread",
    DueAt = standaloneReminder.DueAt.AddHours(1),
};
result = reminderService.UpdateReminder(updatedReminder);
Assert(result.Success && standaloneReminders.Find(standaloneReminder.Id)?.Title == "Buy milk and bread", "Updating an independent reminder must publish the replacement.");
Assert(reminderAlarms.Scheduled.Count == 2 && reminderAlarms.Cancelled.SequenceEqual([100]), "Reminder update must schedule the replacement before cancelling the old alarm.");
result = reminderService.SetReminderCompleted(standaloneReminder.Id, isCompleted: true);
Assert(result.Success && standaloneReminders.Find(standaloneReminder.Id) is { IsCompleted: true, AlarmId: null }, "Completing a reminder must persist completion and clear its alarm handle.");
Assert(reminderAlarms.Cancelled.SequenceEqual([100, 101]), "Completing a reminder must cancel its current alarm.");
result = reminderService.DeleteReminder(standaloneReminder.Id);
Assert(result.Success && standaloneReminders.Find(standaloneReminder.Id) is null, "Deleting an independent reminder must remove it from the shared repository.");

var restoreReminder = CalendarReminder.Create(
    "reminder-restore",
    "Restore me",
    new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
    string.Empty) with { AlarmId = 55 };
var restorePersistence = new RecordingPersistence
{
    Loaded = new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [calendarEvent], [restoreReminder]),
};
var restoreEvents = new CalendarEventRepository([]);
var restoreReminders = new CalendarReminderRepository([]);
var restoreAlarms = new RecordingAlarmScheduler();
var restoreService = new CalendarCommandService(restoreEvents, restoreReminders, restorePersistence, restoreAlarms);
result = restoreService.Restore();
Assert(result.Success && restoreEvents.ResolveByIds([calendarEvent.Id]).Events.Count == 1, "Restore must repopulate the shared event repository.");
Assert(restoreReminders.Find(restoreReminder.Id) is { AlarmId: 100 }, "Restore must replace stale persisted alarm handles with newly scheduled handles.");
Assert(
    restoreAlarms.Cancelled.SequenceEqual([55]) && restorePersistence.Saved.Count == 1,
    "Restore must cancel only persisted app-owned alarm handles before persisting reconciled handles.");

Console.WriteLine("Calendar.UseCases.Tests: PASS");

sealed class RecordingPersistence : ICalendarPersistence
{
    public List<CalendarStoreDocument> Saved { get; } = [];
    public CalendarStoreDocument Loaded { get; set; } = new(CalendarStoreDocument.CurrentSchemaVersion, [], []);

    public CalendarStoreDocument Load() => Loaded;

    public void Save(CalendarStoreDocument document) => Saved.Add(document);
}

sealed class RecordingAlarmScheduler : IReminderAlarmScheduler
{
    private int _nextId = 100;
    public List<CalendarReminder> Scheduled { get; } = [];
    public List<int> Cancelled { get; } = [];

    public int? Schedule(CalendarReminder reminder)
    {
        Scheduled.Add(reminder);
        return _nextId++;
    }

    public void Cancel(int alarmId) => Cancelled.Add(alarmId);
}
