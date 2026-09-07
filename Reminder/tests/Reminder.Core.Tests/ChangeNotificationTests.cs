using Reminder.Domain;
using Reminder.Persistence;
using Reminder.UseCases;

internal static class ChangeNotificationTests
{
    internal static void Run()
    {
        var now = DateTimeOffset.Parse("2027-01-10T10:00:00Z");
        var store = new Store();
        var resources = new Resources();
        var service = new ScheduleService(store, resources, () => now);
        var count = 0;
        var consistent = true;
        service.Changed += () => { count++; consistent &= service.Snapshot.Reminders.SequenceEqual(store.Document.Reminders); };
        var item = ReminderItem.Create("changed", "Live", now.AddHours(1), "");
        if (!service.CreateReminder(item).Success || count != 1 || !consistent) throw new Exception("Notify after persistence and snapshot publication");
        store.Fail = true;
        if (service.UpdateReminder(item with { Title = "Failed" }).Success || count != 1) throw new Exception("Failed saves must not notify");
        store.Fail = false;
        service.Changed += () => throw new Exception("terminated observer");
        var laterObserver = 0;
        service.Changed += () => laterObserver++;
        var result = service.UpdateReminder(item with { Title = "Committed" });
        var committed = service.Snapshot.Reminders.Single();
        if (!result.Success || count != 2 || !consistent || committed.Title != "Committed" || laterObserver != 1 ||
            resources.Cancelled.Contains(committed.ResourceHandle!))
            throw new Exception("A UI observer must not fail or compensate a committed mutation");
        Console.WriteLine("Reminder ChangeNotificationTests PASS: commit ordering, failed saves, observer isolation");
    }
    private sealed class Resources : IScheduleResourceManager
    {
        private int _next;
        public HashSet<string> Cancelled = [];
        public string CreateReminder(ReminderItem reminder) => $"owned-reminder-{++_next}";
        public string CreateReservation(ReservationItem reservation) => $"owned-reservation-{++_next}";
        public void Cancel(string handle) => Cancelled.Add(handle);
    }
    private sealed class Store : IScheduleStore
    {
        public bool Fail;
        public ScheduleDocument Document = new(ScheduleDocument.CurrentSchemaVersion, [], []);
        public ScheduleDocument Load() => Document;
        public void Save(ScheduleDocument value) { if (Fail) throw new IOException("disk full"); Document = value; }
    }
}
