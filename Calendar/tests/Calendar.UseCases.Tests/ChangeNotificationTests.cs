using Calendar.Domain;
using Calendar.Persistence;
using Calendar.UseCases;

internal static class ChangeNotificationTests
{
    internal static void Run()
    {
        var events = new CalendarEventRepository([]);
        var reminders = new CalendarReminderRepository([]);
        var persistence = new Store();
        var service = new CalendarCommandService(events, reminders, persistence, new RecordingAlarmScheduler());
        var changed = typeof(CalendarCommandService).GetEvent("Changed")
            ?? throw new Exception("Committed Calendar changes must notify the running UI.");
        var notifications = 0;
        var consistent = true;
        Action observer = () =>
        {
            notifications++;
            if (!events.Snapshot().SequenceEqual(persistence.Document.Events) ||
                !reminders.Snapshot().OrderBy(x => x.Id).SequenceEqual(persistence.Document.Reminders.OrderBy(x => x.Id)))
                consistent = false;
        };
        changed.AddEventHandler(service, observer);
        var start = DateTimeOffset.Parse("2027-01-10T10:00:00Z");
        var item = CalendarEvent.Create("live", "Live", start, start.AddHours(1), "", "");
        Check(service.CreateEvent(item, [10]), 1);
        Check(service.UpdateEvent(item with { Title = "Updated" }, []), 2);
        if (service.CreateEvent(item, []).Success || notifications != 2) throw new Exception("Rejected changes must not notify.");
        persistence.Fail = true;
        if (service.UpdateEvent(item, []).Success || notifications != 2) throw new Exception("Failed saves must not notify.");
        persistence.Fail = false;
        Check(service.DeleteEvent(item.Id), 3);
        var reminder = CalendarReminder.Create("rem", "Reminder", start, "");
        Check(service.CreateReminder(reminder), 4);
        Check(service.UpdateReminder(reminder with { Title = "New title" }), 5);
        Check(service.SetReminderCompleted(reminder.Id, true), 6);
        Check(service.DeleteReminder(reminder.Id), 7);
        Check(service.Restore(), 8);
        // A view observer must not turn an already committed mutation into failure.
        changed.AddEventHandler(service, (Action)(() => throw new Exception("view disposed")));
        var laterObserver = 0;
        changed.AddEventHandler(service, (Action)(() => laterObserver++));
        Check(service.CreateEvent(item, []), 9);
        if (laterObserver != 1) throw new Exception("A failed observer must not suppress subsequent subscribers.");
        Console.WriteLine("ChangeNotificationTests PASS: committed CRUD/restore, failed saves, observer isolation");
        void Check(CalendarCommandResult result, int expected)
        {
            if (!result.Success || notifications != expected || !consistent) throw new Exception($"Expected {expected} committed notifications, got {notifications}: {result.Reason}");
        }
    }
    private sealed class Store : ICalendarPersistence
    {
        public bool Fail;
        public CalendarStoreDocument Document = new(CalendarStoreDocument.CurrentSchemaVersion, [], []);
        public CalendarStoreDocument Load() => Document;
        public void Save(CalendarStoreDocument value) { if (Fail) throw new IOException("disk full"); Document = value; }
    }
}
