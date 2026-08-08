using Calendar.Domain;

var seoulTestZone = TimeZoneInfo.CreateCustomTimeZone("CalendarTests+09", TimeSpan.FromHours(9), "CalendarTests+09", "CalendarTests+09");
var localBoundary = CalendarDateBoundary.AtStartOfDay(new DateOnly(2026, 8, 10), seoulTestZone);
if (localBoundary.Offset != TimeSpan.FromHours(9) || localBoundary.Date != new DateTime(2026, 8, 10))
{
    throw new InvalidOperationException("Calendar date boundaries must use the explicit calendar timezone rather than UTC midnight.");
}

if (!CalendarSearchQueryAdapter.TryCreate(
        "Studio",
        "2026-08-10T00:00:00+09:00",
        "2026-08-11T00:00:00+09:00",
        0,
        false,
        false,
        false,
        out var defaultFieldCriteria,
        out var defaultFieldError) ||
    defaultFieldCriteria is null ||
    !defaultFieldCriteria.SearchTitle ||
    !defaultFieldCriteria.SearchLocation ||
    !defaultFieldCriteria.SearchNote)
{
    throw new InvalidOperationException($"Omitted typed search selectors must default to all fields: {defaultFieldError}");
}

if (CalendarSearchQueryAdapter.TryCreate(
        "Studio",
        "2026-08-10T00:00:00",
        "2026-08-11T00:00:00+09:00",
        20,
        true,
        false,
        false,
        out _,
        out _))
{
    throw new InvalidOperationException("Typed search timestamps without an explicit UTC offset must be rejected.");
}

var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
var end = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
var calendarEvent = CalendarEvent.Create(
    id: "event-standup",
    title: "Daily stand-up",
    start: start,
    end: end,
    note: "Engineering sync",
    location: "Studio");

if (calendarEvent.Id != "event-standup" || calendarEvent.Duration != TimeSpan.FromHours(1))
{
    throw new InvalidOperationException("CalendarEvent must retain its stable ID and calculate its duration.");
}

var lunch = CalendarEvent.Create(
    id: "event-lunch",
    title: "Lunch",
    start: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
    end: new DateTimeOffset(2026, 8, 10, 13, 0, 0, TimeSpan.Zero),
    note: string.Empty,
    location: "Cafeteria");
var overnight = CalendarEvent.Create(
    id: "event-overnight",
    title: "Release window",
    start: new DateTimeOffset(2026, 8, 9, 23, 0, 0, TimeSpan.Zero),
    end: new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero),
    note: string.Empty,
    location: "Operations");
var nextDay = CalendarEvent.Create(
    id: "event-next-day",
    title: "Planning",
    start: new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero),
    end: new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero),
    note: string.Empty,
    location: "Studio");
var repository = new CalendarEventRepository([calendarEvent, lunch, overnight, nextDay]);
var resolution = repository.ResolveByIds(["event-lunch", "event-missing", "event-standup"]);

if (resolution.Events.Select(item => item.Id).SequenceEqual(["event-lunch", "event-standup"]) is false ||
    resolution.UnresolvedIds.SequenceEqual(["event-missing"]) is false)
{
    throw new InvalidOperationException("Calendar event lookup must return resolved events in request order and report unresolved IDs.");
}

var augustTenth = repository.GetEventsOverlapping(
    new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero));
if (augustTenth.Select(item => item.Id).SequenceEqual(["event-overnight", "event-standup", "event-lunch"]) is false)
{
    throw new InvalidOperationException("Calendar day lookup must include overlapping events and return chronological results.");
}

var review = CalendarEvent.Create(
    id: "event-review",
    title: "Design review",
    start: new DateTimeOffset(2026, 8, 10, 15, 0, 0, TimeSpan.Zero),
    end: new DateTimeOffset(2026, 8, 10, 16, 0, 0, TimeSpan.Zero),
    note: "Candidate B",
    location: "Studio");

if (!repository.TryAdd(review))
{
    throw new InvalidOperationException("Adding an event with an unused stable ID must succeed.");
}

if (repository.TryAdd(review with { Title = "Duplicate" }))
{
    throw new InvalidOperationException("Adding an event with an existing stable ID must be rejected.");
}

var addedResolution = repository.ResolveByIds(["event-review"]);
if (addedResolution.Events.Count != 1 || addedResolution.Events[0].Title != "Design review")
{
    throw new InvalidOperationException("A rejected duplicate add must not overwrite the stored event.");
}

if (!repository.TryUpdate(review with { Title = "Design review (final)", Location = "Lab" }))
{
    throw new InvalidOperationException("Updating an existing event must succeed.");
}

var updatedResolution = repository.ResolveByIds(["event-review"]);
if (updatedResolution.Events[0].Title != "Design review (final)" || updatedResolution.Events[0].Location != "Lab")
{
    throw new InvalidOperationException("An update must replace the stored event.");
}

if (repository.TryUpdate(review with { Id = "event-unknown" }))
{
    throw new InvalidOperationException("Updating an unknown stable ID must report not-found instead of inserting.");
}

if (repository.ResolveByIds(["event-unknown"]).UnresolvedIds.Count != 1)
{
    throw new InvalidOperationException("A rejected update must not insert a new event.");
}

if (!repository.TryDelete("event-review"))
{
    throw new InvalidOperationException("Deleting an existing event must succeed.");
}

if (repository.ResolveByIds(["event-review"]).UnresolvedIds.Count != 1)
{
    throw new InvalidOperationException("A deleted event must no longer resolve.");
}

if (repository.TryDelete("event-review") || repository.TryDelete("event-unknown"))
{
    throw new InvalidOperationException("Deleting a missing stable ID must report not-found instead of succeeding silently.");
}

if (repository.Search("STUDIO").Select(item => item.Id).SequenceEqual(["event-standup", "event-next-day"]) is false)
{
    throw new InvalidOperationException("Search must match locations case-insensitively in chronological order.");
}

if (repository.Search("engineering SYNC").Select(item => item.Id).SequenceEqual(["event-standup"]) is false)
{
    throw new InvalidOperationException("Search must match notes case-insensitively.");
}

if (repository.Search("lunc").Select(item => item.Id).SequenceEqual(["event-lunch"]) is false)
{
    throw new InvalidOperationException("Search must match partial titles case-insensitively.");
}

if (repository.Search("no-such-text").Count != 0)
{
    throw new InvalidOperationException("Search must return an empty result when nothing matches.");
}

if (repository.Search("   ").Count != 4)
{
    throw new InvalidOperationException("A blank search term must return every event in deterministic order.");
}

var augustTenCriteria = CalendarSearchCriteria.Create(
    keyword: string.Empty,
    startInclusive: new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
    endExclusive: new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
    limit: 100);
if (repository.Search(augustTenCriteria).Select(item => item.Id).SequenceEqual(["event-overnight", "event-standup", "event-lunch"]) is false)
{
    throw new InvalidOperationException("Advanced search must use start-inclusive/end-exclusive overlap semantics.");
}

var studioAfterStandup = CalendarSearchCriteria.Create(
    keyword: "STUDIO",
    startInclusive: new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
    endExclusive: new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
    limit: 1);
if (repository.Search(studioAfterStandup).Select(item => item.Id).SequenceEqual(["event-next-day"]) is false)
{
    throw new InvalidOperationException("Advanced search must combine keyword, period, and deterministic result limit filters.");
}

var noteCriteria = CalendarSearchCriteria.Create("engineering sync", null, null, 20);
if (repository.Search(noteCriteria).Select(item => item.Id).SequenceEqual(["event-standup"]) is false)
{
    throw new InvalidOperationException("Advanced search must match notes as well as titles and locations.");
}

var titleOnly = CalendarSearchCriteria.Create(
    "Studio", null, null, 20,
    searchTitle: true, searchLocation: false, searchNote: false);
if (repository.Search(titleOnly).Count != 0)
{
    throw new InvalidOperationException("A title-only search must not match location text.");
}

var locationOnly = CalendarSearchCriteria.Create(
    "Studio", null, null, 20,
    searchTitle: false, searchLocation: true, searchNote: false);
if (repository.Search(locationOnly).Select(item => item.Id).SequenceEqual(["event-standup", "event-next-day"]) is false)
{
    throw new InvalidOperationException("A location-only search must match location text without consulting other fields.");
}

try
{
    CalendarSearchCriteria.Create(
        "Studio", null, null, 20,
        searchTitle: false, searchLocation: false, searchNote: false);
    throw new InvalidOperationException("Advanced search must require at least one selected text field.");
}
catch (ArgumentException)
{
}

try
{
    CalendarSearchCriteria.Create(
        "invalid",
        new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
        20);
    throw new InvalidOperationException("Advanced search must reject an inverted period.");
}
catch (ArgumentException)
{
}

var snapshot = repository.Snapshot();
if (snapshot.Select(item => item.Id).SequenceEqual(
        ["event-overnight", "event-standup", "event-lunch", "event-next-day"]) is false)
{
    throw new InvalidOperationException("Snapshot must return every event ordered by start then stable ID.");
}

repository.TryDelete("event-next-day");
if (snapshot.Count != 4)
{
    throw new InvalidOperationException("A snapshot must be an immutable copy unaffected by later mutations.");
}

repository.ReplaceAll(snapshot);
if (repository.Snapshot().Select(item => item.Id).SequenceEqual(snapshot.Select(item => item.Id)) is false)
{
    throw new InvalidOperationException("ReplaceAll must restore the repository to a previously captured snapshot.");
}

var reminderDue = new DateTimeOffset(2026, 8, 12, 8, 30, 0, TimeSpan.Zero);
var groceries = CalendarReminder.Create(
    id: "reminder-groceries",
    title: "  Buy groceries  ",
    dueAt: reminderDue,
    note: "  Milk and bread  ");

if (groceries.Id != "reminder-groceries" || groceries.Title != "Buy groceries" ||
    groceries.DueAt != reminderDue || groceries.Note != "Milk and bread")
{
    throw new InvalidOperationException("A reminder must retain its stable ID, due date, and trimmed text.");
}

if (groceries.IsCompleted || groceries.CalendarEventId is not null ||
    groceries.OffsetMinutes is not null || groceries.AlarmId is not null)
{
    throw new InvalidOperationException("A new independent reminder must be incomplete with no link, offset, or alarm.");
}

foreach (var invalid in new (string Id, string Title)[] { ("", "Title"), ("   ", "Title"), ("reminder-x", ""), ("reminder-x", "   ") })
{
    try
    {
        CalendarReminder.Create(invalid.Id, invalid.Title, reminderDue, note: null);
        throw new InvalidOperationException("A reminder must require a non-blank stable ID and title.");
    }
    catch (ArgumentException)
    {
    }
}

if (CalendarReminder.AllowedOffsetMinutes.SequenceEqual([10, 30, 60, 1440]) is false)
{
    throw new InvalidOperationException("Event-linked reminders must offer the 10, 30, 60, and 1440 minute presets.");
}

var eventStart = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
foreach (var offsetMinutes in CalendarReminder.AllowedOffsetMinutes)
{
    var linked = CalendarReminder.CreateForEvent(
        id: $"reminder-standup-{offsetMinutes}",
        title: "Daily stand-up",
        eventStart: eventStart,
        calendarEventId: "event-standup",
        offsetMinutes: offsetMinutes,
        note: null);

    if (linked.CalendarEventId != "event-standup" || linked.OffsetMinutes != offsetMinutes ||
        linked.DueAt != eventStart.AddMinutes(-offsetMinutes) || linked.IsCompleted || linked.AlarmId is not null)
    {
        throw new InvalidOperationException("An event-linked reminder must be due its offset before the event start.");
    }
}

foreach (var rejectedOffset in new[] { 0, -10, 15, 1441 })
{
    try
    {
        CalendarReminder.CreateForEvent(
            "reminder-bad", "Daily stand-up", eventStart, "event-standup", rejectedOffset, note: null);
        throw new InvalidOperationException("An event-linked reminder must reject offsets outside the allowed presets.");
    }
    catch (ArgumentException)
    {
    }
}

foreach (var blankEventId in new[] { "", "   " })
{
    try
    {
        CalendarReminder.CreateForEvent(
            "reminder-bad", "Daily stand-up", eventStart, blankEventId, 10, note: null);
        throw new InvalidOperationException("An event-linked reminder must require a non-blank calendar event ID.");
    }
    catch (ArgumentException)
    {
    }
}

var scheduled = groceries with { AlarmId = 4242 };
if (scheduled.AlarmId != 4242 || groceries.AlarmId is not null)
{
    throw new InvalidOperationException("Alarm metadata must be captured without mutating the original reminder.");
}

var reminders = new CalendarReminderRepository([groceries]);

if (reminders.Find("reminder-groceries")?.Title != "Buy groceries" || reminders.Find("reminder-missing") is not null)
{
    throw new InvalidOperationException("Find must return the stored reminder or null for an unknown stable ID.");
}

var callDentist = CalendarReminder.Create("reminder-dentist", "Call dentist", reminderDue.AddDays(1), note: null);
if (!reminders.TryAdd(callDentist))
{
    throw new InvalidOperationException("Adding a reminder with an unused stable ID must succeed.");
}

if (reminders.TryAdd(callDentist with { Title = "Duplicate" }) ||
    reminders.Find("reminder-dentist")!.Title != "Call dentist")
{
    throw new InvalidOperationException("Adding a duplicate reminder ID must be rejected without overwriting.");
}

if (!reminders.TryUpdate(callDentist with { Title = "Call dentist back", Note = "Reschedule" }) ||
    reminders.Find("reminder-dentist")!.Note != "Reschedule")
{
    throw new InvalidOperationException("Updating an existing reminder must replace the stored record.");
}

if (reminders.TryUpdate(callDentist with { Id = "reminder-unknown" }) || reminders.Find("reminder-unknown") is not null)
{
    throw new InvalidOperationException("Updating an unknown reminder ID must report not-found instead of inserting.");
}

if (!reminders.TryDelete("reminder-dentist") || reminders.Find("reminder-dentist") is not null)
{
    throw new InvalidOperationException("Deleting an existing reminder must succeed and remove it.");
}

if (reminders.TryDelete("reminder-dentist") || reminders.TryDelete("reminder-unknown"))
{
    throw new InvalidOperationException("Deleting a missing reminder ID must report not-found instead of succeeding silently.");
}

var standupReminder = CalendarReminder.CreateForEvent(
    id: "reminder-standup-link",
    title: "Daily stand-up",
    eventStart: eventStart,
    calendarEventId: "event-standup",
    offsetMinutes: 30,
    note: null) with
{
    AlarmId = 77,
};
var lunchReminder = CalendarReminder.CreateForEvent(
    id: "reminder-lunch-link",
    title: "Lunch",
    eventStart: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
    calendarEventId: "event-lunch",
    offsetMinutes: 10,
    note: null);
reminders.TryAdd(standupReminder);
reminders.TryAdd(lunchReminder);

if (reminders.FindByCalendarEventId("event-standup").Select(item => item.Id).SequenceEqual(["reminder-standup-link"]) is false ||
    reminders.FindByCalendarEventId("event-missing").Count != 0 ||
    reminders.FindByCalendarEventId(null).Count != 0)
{
    throw new InvalidOperationException("Linked reminder lookup must return only the reminders for that calendar event.");
}

if (!reminders.TryComplete("reminder-groceries"))
{
    throw new InvalidOperationException("Completing an incomplete reminder must succeed.");
}

var completed = reminders.Find("reminder-groceries")!;
if (!completed.IsCompleted || completed.AlarmId is not null)
{
    throw new InvalidOperationException("Completing a reminder must mark it done and clear its alarm metadata.");
}

if (!reminders.TryReopen("reminder-groceries") || reminders.Find("reminder-groceries")!.IsCompleted)
{
    throw new InvalidOperationException("Reopening a completed reminder must clear its completed state.");
}

if (reminders.TryComplete("reminder-unknown") || reminders.TryReopen("reminder-unknown"))
{
    throw new InvalidOperationException("Completing or reopening an unknown reminder must report not-found.");
}

reminders.TryComplete("reminder-lunch-link");

var reminderSnapshot = reminders.Snapshot();
if (reminderSnapshot.Select(item => item.Id).SequenceEqual(
        ["reminder-standup-link", "reminder-groceries", "reminder-lunch-link"]) is false)
{
    throw new InvalidOperationException(
        "Snapshot must order incomplete reminders by due date and stable ID before completed reminders.");
}

reminders.TryDelete("reminder-groceries");
if (reminderSnapshot.Count != 3)
{
    throw new InvalidOperationException("A reminder snapshot must be an immutable copy unaffected by later mutations.");
}

reminders.ReplaceAll(reminderSnapshot);
if (reminders.Snapshot().Select(item => item.Id).SequenceEqual(reminderSnapshot.Select(item => item.Id)) is false)
{
    throw new InvalidOperationException("ReplaceAll must restore the reminder repository to a captured snapshot.");
}

if (reminders.Search("GROCERIES").Select(item => item.Id).SequenceEqual(["reminder-groceries"]) is false ||
    reminders.Search("milk AND bread").Select(item => item.Id).SequenceEqual(["reminder-groceries"]) is false ||
    reminders.Search("no-such-text").Count != 0 ||
    reminders.Search("  ").Count != 3)
{
    throw new InvalidOperationException(
        "Reminder search must match titles and notes case-insensitively and return all reminders for a blank term.");
}

var concurrentReminders = new CalendarReminderRepository([]);
var concurrentBase = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
var writers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
{
    for (var index = 0; index < 200; index++)
    {
        var id = $"reminder-{worker:D2}-{index:D3}";
        concurrentReminders.TryAdd(CalendarReminder.Create(id, $"Reminder {id}", concurrentBase.AddMinutes(index), note: null));
        concurrentReminders.TryComplete(id);
        concurrentReminders.TryReopen(id);
    }
}));
var readers = Enumerable.Range(0, 4).Select(reader => Task.Run(() =>
{
    for (var index = 0; index < 400; index++)
    {
        var observed = concurrentReminders.Snapshot();
        if (observed.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != observed.Count)
        {
            throw new InvalidOperationException("A reminder snapshot must never contain duplicate stable IDs.");
        }

        if (concurrentReminders.Search("Reminder").Count > observed.Count + 1600)
        {
            throw new InvalidOperationException("Concurrent reminder search must return a consistent copy.");
        }
    }
}));

Task.WaitAll([.. writers, .. readers]);

if (concurrentReminders.Snapshot().Count != 1600)
{
    throw new InvalidOperationException("Concurrent reminder mutations must not lose or duplicate records.");
}

Console.WriteLine("Calendar.Domain.Tests: PASS");
