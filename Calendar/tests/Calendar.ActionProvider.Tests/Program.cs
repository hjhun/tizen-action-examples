using Calendar.Domain;

var standup = CalendarEvent.Create(
    id: "event-standup",
    title: "Daily stand-up",
    start: new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero),
    end: new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
    note: "Engineering sync",
    location: "Studio");
var repository = new CalendarEventRepository([standup]);

AssertAdapter(
    keyword: "engineering",
    startDate: "2026-08-10T00:00:00Z",
    endDate: "2026-08-11T00:00:00Z",
    searchTitle: false,
    searchLocation: false,
    searchNote: false,
    expectedIds: ["event-standup"],
    "Omitted/all-false selectors must normalize to all searchable fields.");

AssertAdapter(
    keyword: "studio",
    startDate: "2026-08-10T00:00:00Z",
    endDate: "2026-08-11T00:00:00Z",
    searchTitle: false,
    searchLocation: true,
    searchNote: false,
    expectedIds: ["event-standup"],
    "Location-only provider input must map to a location-only domain search.");

AssertAdapter(
    keyword: "studio",
    startDate: "2026-08-10T10:00:00+00:00",
    endDate: "2026-08-12T00:00:00+00:00",
    searchTitle: false,
    searchLocation: true,
    searchNote: false,
    expectedIds: [],
    "Provider input must preserve start-inclusive/end-exclusive overlap boundaries.");

if (CalendarSearchQueryAdapter.TryCreate(
        "studio",
        "08/10/2026 00:00:00",
        "2026-08-12T00:00:00Z",
        20,
        false,
        true,
        false,
        out _,
        out _))
{
    throw new InvalidOperationException("Provider input must reject culture-dependent or offset-free timestamps.");
}

Console.WriteLine("Calendar.ActionProvider.Tests: PASS");

void AssertAdapter(
    string keyword,
    string startDate,
    string endDate,
    bool searchTitle,
    bool searchLocation,
    bool searchNote,
    IReadOnlyList<string> expectedIds,
    string failure)
{
    if (!CalendarSearchQueryAdapter.TryCreate(
            keyword,
            startDate,
            endDate,
            20,
            searchTitle,
            searchLocation,
            searchNote,
            out var criteria,
            out var error))
    {
        throw new InvalidOperationException($"{failure} Adapter rejected input: {error}");
    }

    var actualIds = repository.Search(criteria!).Select(calendarEvent => calendarEvent.Id).ToArray();
    if (!actualIds.SequenceEqual(expectedIds))
    {
        throw new InvalidOperationException(failure);
    }
}
