using Reminder.Domain;
using Reminder.Persistence;
using Reminder.ScheduleActionProvider;
using Reminder.UseCases;
using RPCPort.ReminderScheduleActionProvider;

internal static class ResolverContractTests
{
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var now = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
        var items = new[]
        {
            ReminderItem.Create("todo", "Supported", null, null, now),
            ReminderItem.Create("done", "Completed", null, null, now).WithState("Done"),
            ReminderItem.Create("progress", "Progress", null, null, now).WithState("In-progress"),
            ReminderItem.Create("blocked", "Blocked", null, null, now).WithState("Blocked"),
        };
        var store = new CountingStore(new(ScheduleDocument.CurrentSchemaVersion, items, []));
        var resources = new DeterministicReservationSimulator();
        var service = new ScheduleService(store, resources, () => now);
        var before = service.Snapshot;
        var saves = store.Saves;
        var changes = 0;
        service.Changed += () => changes++;
        var custom = new ReminderCustomService(service);

        foreach (var unsupported in new[] { "progress", "blocked" })
        {
            var status = custom.GetReminderByIds(
                ["todo", "missing", unsupported, "done", "todo"],
                out var result, out var unresolved);
            Assert(!status.Success && status.Reason.StartsWith("unavailable:"),
                "Mixed state results must fail explicitly.");
            Assert(result.Count == 0 && unresolved.Count == 0,
                "Failure must publish neither partial results nor unresolved IDs.");
        }

        var supported = custom.GetReminderByIds(
            ["done", "todo", "missing", "todo", "missing"],
            out var resolvedItems, out var missing);
        Assert(supported.Success, "Supported states must resolve successfully.");
        Assert(resolvedItems.Select(item => item.Id).SequenceEqual(new[] { "done", "todo", "todo" }),
            "Resolver must preserve supported ID order and duplicates.");
        Assert(missing.SequenceEqual(new[] { "missing", "missing" }),
            "Supported resolution must preserve unresolved order and duplicates.");
        Assert(resolvedItems.All(item => item.DueDate is null),
            "Custom resolver must preserve absent due dates.");

        var standard = new ReminderScheduleService(service);
        var omitted = standard.Search(new() { Limit = 1 }, out var omittedResults);
        Assert(!omitted.Success && omittedResults.Count == 0,
            "Omitted State must not silently exclude unsupported active matches beyond Limit.");
        var todo = standard.Search(new() { State = new() { State = "To-do" }, Limit = 1 }, out var todoResults);
        Assert(todo.Success && todoResults.Count == 1 && todoResults[0].Id == "todo",
            "Explicit To-do selects supported matches before limiting.");
        Assert(todoResults[0].DueDate is null, "Standard search must preserve absent due dates.");
        var done = standard.Search(new() { State = new() { State = "Done" } }, out var doneResults);
        Assert(done.Success && doneResults.Select(item => item.Id).SequenceEqual(new[] { "done" }),
            "Explicit Done must preserve completed matches.");
        var filter = standard.Search(new() { Profiles = [] }, out var filtered);
        Assert(!filter.Success && filtered.Count == 0,
            "Present unsupported empty filter must fail explicitly.");
        var add = standard.Add(new() { Title = "Not added" }, out var added);
        Assert(!add.Success && added is null, "Unavailable Add must omit optional result.");
        var update = standard.Update(new() { Id = "todo", Title = "Not updated" }, out _);
        Assert(!update.Success, "Unavailable Update must fail before mutation.");

        Assert(service.Snapshot.Reminders.SequenceEqual(before.Reminders) && store.Saves == saves
            && changes == 0 && resources.CreatedHandles.Count == 0 && resources.CancelledHandles.Count == 0,
            "Boundary rejection and readonly resolution must have no domain or resource effects.");
    }

    private sealed class CountingStore(ScheduleDocument document) : IScheduleStore
    {
        public int Saves { get; private set; }
        public ScheduleDocument Load() => document;
        public void Save(ScheduleDocument value) { Saves++; document = value; }
    }
}
