using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarSearchState(
    string Keyword,
    DateOnly StartDate,
    DateOnly EndDateExclusive,
    IReadOnlyList<string> ResultEventIds,
    bool SearchTitle,
    bool SearchLocation,
    bool SearchNote)
{
    public bool HasApplied { get; init; }

    public long AppliedRepositoryVersion { get; init; } = -1;

    public bool CanApply => EndDateExclusive > StartDate &&
        (SearchTitle || SearchLocation || SearchNote);

    public string? ValidationMessage => CanApply
        ? null
        : EndDateExclusive <= StartDate
            ? "Exclusive end date must be after start date."
            : "Select at least one field: Title, Location, or Notes.";

    public static CalendarSearchState Create(DateOnly visibleMonth)
    {
        var start = new DateOnly(visibleMonth.Year, visibleMonth.Month, 1);
        return new CalendarSearchState(
            string.Empty,
            start,
            start.AddMonths(1),
            Array.Empty<string>(),
            SearchTitle: true,
            SearchLocation: true,
            SearchNote: true);
    }

    public CalendarSearchState WithKeyword(string? keyword) => this with
    {
        Keyword = keyword?.Trim() ?? string.Empty,
        ResultEventIds = Array.Empty<string>(),
        HasApplied = false,
    };

    public CalendarSearchState WithPeriod(DateOnly startDate, DateOnly endDateExclusive) => this with
    {
        StartDate = startDate,
        EndDateExclusive = endDateExclusive,
        ResultEventIds = Array.Empty<string>(),
        HasApplied = false,
    };

    public CalendarSearchState WithFields(bool searchTitle, bool searchLocation, bool searchNote) => this with
    {
        SearchTitle = searchTitle,
        SearchLocation = searchLocation,
        SearchNote = searchNote,
        ResultEventIds = Array.Empty<string>(),
        HasApplied = false,
    };

    public CalendarSearchState Apply(CalendarEventRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!CanApply)
        {
            return this with { ResultEventIds = Array.Empty<string>(), HasApplied = false };
        }

        var start = CalendarDateBoundary.AtStartOfDay(StartDate);
        var endExclusive = CalendarDateBoundary.AtStartOfDay(EndDateExclusive);
        var criteria = CalendarSearchCriteria.Create(
            Keyword,
            start,
            endExclusive,
            100,
            SearchTitle,
            SearchLocation,
            SearchNote);
        var snapshot = repository.SearchWithVersion(criteria);
        return this with
        {
            ResultEventIds = snapshot.Events.Select(calendarEvent => calendarEvent.Id).ToArray(),
            HasApplied = true,
            AppliedRepositoryVersion = snapshot.RepositoryVersion,
        };
    }
}
