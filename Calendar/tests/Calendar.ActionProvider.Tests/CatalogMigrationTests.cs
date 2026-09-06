using System.Text.Json;
using Calendar.Domain;

internal static class CatalogMigrationTests
{
    internal static void Run()
    {
        var start = new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 120).Select(i => CalendarEvent.Create(
            $"event-{i}", $"Event {i}", start.AddHours(i), start.AddHours(i + 1), "Note", "Studio")).ToArray();
        var repository = new CalendarEventRepository(events);
        Check(CalendarSearchQueryAdapter.TryCreate(null, null, null, 1, true, true, true,
            out var byId, out _, id: "event-119", category: "Calendar"));
        Check(repository.Search(byId!).Single().Id == "event-119");
        Check(CalendarSearchQueryAdapter.TryCreate("Studio", null, null, 200, true, true, true,
            out var bounded, out _));
        Check(repository.Search(bounded!).Count == 100);
        Check(CalendarSearchQueryAdapter.TryCreate(null, null, null, 0, true, true, true,
            out var missing, out _, id: "missing"));
        Check(repository.Search(missing!).Count == 0);
        Check(!CalendarSearchQueryAdapter.TryCreate(null, null, null, 1, true, true, true,
            out _, out _, id: new string('x', 257)));
        Check(!CalendarSearchQueryAdapter.TryCreate(null, null, null, 1, true, true, true,
            out _, out _, category: "Music"));
        Check(!CalendarSearchQueryAdapter.TryCreate(new string('x', 513), null, null, 1, true, true, true,
            out _, out _));
        Check(!CalendarSearchQueryAdapter.TryCreate(null, "2026-09-07T00:00:00Z", "2026-09-06T00:00:00Z", 1,
            true, true, true, out _, out _));

        var resolution = repository.ResolveByIds(["event-119", "missing", "event-0", "event-119"]);
        Check(resolution.Events.Select(e => e.Id).SequenceEqual(["event-119", "event-0", "event-119"]));
        Check(resolution.UnresolvedIds.SequenceEqual(["missing"]));

        var single = CalendarA2UiPresentations.Create(events[0]);
        Check(CalendarA2UiPresentations.Create([events[0]]) == single);
        foreach (var input in new[] { Array.Empty<CalendarEvent>(), events.Take(2).ToArray(), events.Take(100).ToArray() })
        {
            var presentation = CalendarA2UiPresentations.Create(input);
            using var template = JsonDocument.Parse(presentation.Template);
            using var document = JsonDocument.Parse(presentation.Document);
            Check(template.RootElement.GetProperty("surfaceUpdate").GetProperty("surfaceId").GetString() ==
                document.RootElement.GetProperty("dataModelUpdate").GetProperty("surfaceId").GetString());
            var components = template.RootElement.GetProperty("surfaceUpdate").GetProperty("components");
            Check(components.EnumerateArray().Select(c => c.GetProperty("id").GetString()).Distinct().Count() == components.GetArrayLength());
            var value = document.RootElement.GetProperty("dataModelUpdate").GetProperty("value");
            Check(value.EnumerateObject().Count() == input.Length);
            for (var i = 0; i < input.Length; i++) Check(value.GetProperty($"event-{i}").GetProperty("id").GetString() == input[i].Id);
        }
        try { CalendarA2UiPresentations.Create(events); throw new InvalidOperationException("Unbounded presentation accepted."); }
        catch (ArgumentException) { }

        foreach (var name in new[] { "TizenEntityCalendarEvent", "TizenEntityCalendar" })
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, object> { [name] = new
            {
                Id = events[0].Id, Title = events[0].Title, StartDate = events[0].Start.ToString("O"),
                EndDate = events[0].End.ToString("O"), Note = events[0].Note, Location = events[0].Location,
            } });
            Check(CalendarA2UiPresentations.TryCreateFromGeneratedEntityJson(json, out var converted, events[0].Id));
            Check(converted == single);
            Check(!CalendarA2UiPresentations.TryCreateFromGeneratedEntityJson(json, out _, "wrong-id"));
        }
        foreach (var bad in new[] { "[]", "null", "{", "{\"TizenEntityCalendarEvent\":null}", new string('x', 65537) })
            Check(!CalendarA2UiPresentations.TryCreateFromGeneratedEntityJson(bad, out _));

        var reminder = CalendarReminder.Create("reminder-1", "Call", start, "Bring notes");
        foreach (var state in new[] { "To-do", "In-progress", "Blocked", "Done" })
        {
            var changed = reminder.WithState(state);
            Check(changed.State == state && changed.IsCompleted == (state == "Done"));
            using var json = JsonDocument.Parse(CalendarA2UiPresentations.CreateReminder(changed).Document);
            Check(json.RootElement.GetProperty("dataModelUpdate").GetProperty("value").GetProperty("state").GetString() == state);
        }
        try { reminder.WithState("Unknown"); throw new InvalidOperationException("Unknown state accepted."); }
        catch (ArgumentException) { }
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Action catalog migration regression.");
    }
}
