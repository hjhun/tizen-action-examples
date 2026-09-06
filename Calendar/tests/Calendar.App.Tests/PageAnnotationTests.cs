using System.Text.Json;
using ActionExamples.ViewAnnotations;
using Calendar.App;
using Calendar.Domain;
using Reminder.App;

internal static class PageAnnotationTests
{
    internal static void Run()
    {
        var home = CalendarInteractionState.Create(CalendarUiState.Create(new DateOnly(2026, 9, 6)));
        var item = CalendarEvent.Create("event-1", "Planning", DateTimeOffset.Parse("2026-09-06T10:00:00Z"), DateTimeOffset.Parse("2026-09-06T11:00:00Z"), "Private detail", "Room 1");
        var reminder = CalendarReminder.Create("reminder-1", "Follow up", item.End, "Draft note");
        var detail = home.OpenEventDetail(item.Id);
        var reminders = home.OpenReminderList();
        var search = home.OpenSearch();
        var states = Enum.GetValues<CalendarViewMode>().Select(mode => home with { Calendar = home.Calendar.ChangeViewMode(mode) })
            .Concat(new[] { search, detail, detail.OpenEventEditor(item, []), home.OpenNewEvent(), detail.RequestEventDelete(),
                reminders, reminders.OpenNewReminder(item.End), reminders.OpenReminderEditor(reminder), reminders.OpenReminderEditor(reminder).RequestReminderDelete() }).ToArray();
        var pages = states.Select(CalendarAnnotationPage.Create).ToArray();
        Check(pages.Select(x => x.Id).Distinct().Count() == pages.Length, "Calendar surfaces/new/edit must be distinguishable.");
        var previousPage = CalendarAnnotationPage.Create(home);
        var nextPage = CalendarAnnotationPage.Create(home with { Calendar = home.Calendar.MovePeriod(1) });
        Check(previousPage.Id == nextPage.Id && JsonSerializer.Serialize(previousPage.State) != JsonSerializer.Serialize(nextPage.State), "Changing the month must change context while retaining stable surface identity.");
        var applied = CalendarAnnotationPage.Create(search with { Search = search.Search! with { HasApplied = true, Keyword = "Planning", ResultEventIds = ["offscreen-id"] } });
        var appliedJson = JsonSerializer.Serialize(applied.State);
        Check(appliedJson.Contains("Planning") && appliedJson.Contains("resultCount") && !appliedJson.Contains("offscreen-id"), "Search context must expose conditions/count, not invisible result entities.");

        var store = new CurrentViewStore();
        string? oldId = null;
        foreach (var page in pages)
        {
            var snapshot = Context(page.Id, page.State);
            var control = snapshot with { Id = page.Id + ":control:Save", IsFocused = true, Width = 120, Height = 60 };
            store.Publish([snapshot, control]);
            Check(store.All().Count == 2 && store.Focused()?.Id == control.Id, "Focused control must also appear in the current annotated set.");
            Check(store.Find(control.Id) == store.Focused(), "FindById and focus must agree.");
            if (oldId is not null) Check(store.Find(oldId) is null, "Previous page views must disappear.");
            Check(ViewSnapshotPresentation.TryCreate(snapshot.EntityType, snapshot.EntityId, snapshot.EntityInfo, out var template, out var document), "Every page context must support View_ToPresentation.");
            Check(JsonDocument.Parse(template).RootElement.GetProperty("surfaceUpdate").GetProperty("components").GetArrayLength() > 1, "Presentation needs semantic fields.");
            Check(JsonDocument.Parse(document).RootElement.GetProperty("dataModelUpdate").GetProperty("value").EnumerateObject().Any(), "Presentation must contain current page data.");
            oldId = snapshot.Id;
        }
        foreach (var section in new[] { "Today", "Upcoming", "Overdue", "Completed", "All", "Reservations" })
        {
            var list = ReminderAnnotationPage.Create(section, false, false, null, "", "All");
            var selected = ReminderAnnotationPage.Create(section, false, false, "selected", "", "All");
            var editor = ReminderAnnotationPage.Create(section, true, false, "selected", "", "All");
            var create = ReminderAnnotationPage.Create(section, true, true, null, "", "All");
            Check(new[] { list.Id, selected.Id, editor.Id, create.Id }.Distinct().Count() == 4, "Reminder page identity must include its mode.");
            var filtered = ReminderAnnotationPage.Create(section, false, false, null, "meeting", "Morning");
            Check(list.Id == filtered.Id && JsonSerializer.Serialize(list.State) != JsonSerializer.Serialize(filtered.State), "Reminder search/time filters must update the current context.");
            store.Publish([Context(list.Id, list.State)]);
            Check(store.All().Count == 1 && store.Focused() is null, "Empty lists retain page context without fabricating focus.");
        }
        var valid = Context("valid", new { surface = "Month" });
        store.Publish([valid, valid, valid with { Id = "zero", Width = 0 }, valid with { Id = "nan", ScreenX = double.NaN }, valid with { Id = "infinite", Height = double.PositiveInfinity }]);
        Check(store.All().Count == 1, "Reject duplicate/unmeasured/non-finite views.");
        var copy = store.All().ToArray(); copy[0] = valid with { Id = "changed" };
        Check(store.Find("valid") is not null, "Readers cannot modify published state.");
        var changed = valid with { EntityInfo = Context("valid", new { surface = "Week" }).EntityInfo };
        store.Publish([changed]);
        Check(store.Resolve("valid", valid.EntityType, valid.EntityId) == changed, "View presentation resolves current data instead of trusting a caller's stale snapshot.");
        Check(store.Resolve("valid", "Tizen.Entity.Reminder", valid.EntityId) is null &&
            store.Resolve("valid", valid.EntityType, "other") is null, "Reject mismatched annotation identity.");
        store.Clear();
        Check(store.Resolve("valid", valid.EntityType, valid.EntityId) is null, "Hidden and removed views cannot be converted using an old snapshot.");
        Check(store.All().Count == 0 && store.Focused() is null && store.Find("valid") is null, "Pause/removal must clear every query path.");
        Check(!ViewSnapshotPresentation.TryCreate("Tizen.Entity", "wrong", valid.EntityInfo, out _, out _), "Reject mismatched entity IDs.");
        foreach (var malformed in new[] { "null", "[]", "{", new string('x', 65537), "{\"TizenEntity\":{\"Id\":\"valid\",\"Extra\":\"{}\"}}" })
            Check(!ViewSnapshotPresentation.TryCreate("Tizen.Entity", "valid", malformed, out _, out _), "Malformed annotations must produce typed failure.");
        Console.WriteLine("PageAnnotationTests: PASS (Calendar surfaces, Reminder sections/modes, publication lifecycle, focus, bounds, presentation)");
    }

    private static CurrentViewSnapshot Context(string id, object page) => new(id, "Page", id, "Tizen.Entity", id,
        JsonSerializer.Serialize(new { TizenEntity = new { Id = id, Extra = JsonSerializer.Serialize(new { schemaVersion = 1, page }) } }))
        { ScreenX = 20, ScreenY = 30, Width = 1920, Height = 1080 };

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
