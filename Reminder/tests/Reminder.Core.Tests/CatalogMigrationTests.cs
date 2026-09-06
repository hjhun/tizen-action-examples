using Reminder.Domain;
using Reminder.Persistence;
using Reminder.UseCases;

internal static class CatalogMigrationTests
{
    internal static void Run()
    {
        var store = new MemoryScheduleStore();
        var service = new ScheduleService(store, new DeterministicReservationSimulator());
        for (var i = 0; i < 105; i++)
            Check(service.CreateReminder(ReminderItem.Create($"item-{i:D3}", $"Task {i}", null, "")).Success, "fixture creation");
        var query = ReminderContract.Query("item-104", "", "Reminder", 1);
        Check(service.SearchReminders(query).Single().Id == "item-104", "ID filter before limit");
        foreach (var category in new[] { "", "Reminder", "Tizen.Action.Reminder", "org.tizen.actionexamples.reminder" })
            Check(service.SearchReminders(ReminderContract.Query("", "", category, 999)).Count == 100, "bounded domain query");
        Reject(() => ReminderContract.Query("", "", "Completed", 10));
        Reject(() => ReminderContract.Query(new string('x', 129), "", "", 1));
        Reject(() => ReminderContract.Query("", new string('x', 201), "", 1));
        var resolved = service.ResolveReminderIds(["item-104", "missing", "item-000", "item-104"]);
        Check(resolved.Items.Select(x => x.Id).SequenceEqual(["item-104", "item-000", "item-104"]), "resolver order and duplicates");
        Check(resolved.UnresolvedIds.SequenceEqual(["missing"]), "unresolved IDs");
        Reject(() => service.ResolveReminderIds(Enumerable.Repeat("item-000", 101).ToArray()));
        Reject(() => service.ResolveReminderIds([""]));
        foreach (var state in new[] { "To-do", "In-progress", "Blocked", "Done" })
        {
            var item = ReminderContract.Item("item-000", "State test", "", "note", state);
            Check(item.State == state && item.Completed == (state == "Done"), "state conversion");
            Check(service.UpdateReminder(item).Success, "state update");
            Check(store.Load().Reminders.Single(x => x.Id == item.Id).State == state, "persisted state");
        }
        Reject(() => ReminderContract.Item("id", "title", "", "", "Unknown"));
        Reject(() => ReminderContract.Item("id", "title", "2026-09-08T12:00:00", "", "To-do"));
        var path = Path.Combine(Path.GetTempPath(), $"reminder-state-{Guid.NewGuid():N}.json");
        try
        {
            var json = new JsonScheduleStore(path);
            var blocked = ReminderContract.Item("blocked", "Persist state", "", "", "Blocked");
            json.Save(new ScheduleDocument(1, [blocked], []));
            Check(json.Load().Reminders.Single().State == "Blocked", "JSON state round trip");
        }
        finally { File.Delete(path); }
        Console.WriteLine("CatalogMigrationTests: PASS (states, domain query, ID-before-limit, ordered resolver, persistence)");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid contract input accepted.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
