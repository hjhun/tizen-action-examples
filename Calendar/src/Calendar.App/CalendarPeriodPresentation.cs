using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarPeriodEventPresentation(
    string EventId,
    string Title,
    string TimeText,
    string Location,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    CalendarEventColorRole ColorRole);

public sealed record CalendarPeriodDayPresentation(
    DateOnly Date,
    string DayLabel,
    bool IsToday,
    bool IsSelected,
    IReadOnlyList<CalendarPeriodEventPresentation> Events);

public sealed record CalendarPeriodPresentation(
    CalendarViewMode ViewMode,
    DateOnly RangeStart,
    DateOnly RangeEndExclusive,
    string Title,
    IReadOnlyList<CalendarPeriodDayPresentation> Days,
    string EmptyStateText)
{
    public bool IsEmpty => Days.Count == 0 || Days.All(day => day.Events.Count == 0);

    public static CalendarPeriodPresentation Create(
        CalendarUiState state,
        CalendarEventRepository repository,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repository);

        var (rangeStart, rangeEndExclusive, title) = GetRange(state);
        var days = Enumerable.Range(0, rangeEndExclusive.DayNumber - rangeStart.DayNumber)
            .Select(offset => rangeStart.AddDays(offset))
            .Select(date => CreateDay(date, state.SelectedDate, today, repository))
            .ToArray();

        if (state.ViewMode == CalendarViewMode.Agenda)
        {
            days = days.Where(day => day.Events.Count > 0).ToArray();
        }

        return new CalendarPeriodPresentation(
            state.ViewMode,
            rangeStart,
            rangeEndExclusive,
            title,
            days,
            "No events in this period");
    }

    private static (DateOnly Start, DateOnly EndExclusive, string Title) GetRange(CalendarUiState state)
    {
        return state.ViewMode switch
        {
            CalendarViewMode.Week => GetWeekRange(state.SelectedDate),
            CalendarViewMode.Day => (
                state.SelectedDate,
                state.SelectedDate.AddDays(1),
                state.SelectedDate.ToString("dddd, MMMM d, yyyy")),
            CalendarViewMode.Agenda => (
                state.VisibleMonth,
                state.VisibleMonth.AddMonths(1),
                $"{state.VisibleMonth:MMMM yyyy} agenda"),
            _ => (
                state.VisibleMonth,
                state.VisibleMonth.AddMonths(1),
                state.VisibleMonth.ToString("MMMM yyyy")),
        };
    }

    private static (DateOnly Start, DateOnly EndExclusive, string Title) GetWeekRange(DateOnly selectedDate)
    {
        var start = selectedDate.AddDays(-(int)selectedDate.DayOfWeek);
        var endExclusive = start.AddDays(7);
        return (start, endExclusive, $"{start:MMM d} – {endExclusive.AddDays(-1):MMM d, yyyy}");
    }

    private static CalendarPeriodDayPresentation CreateDay(
        DateOnly date,
        DateOnly selectedDate,
        DateOnly today,
        CalendarEventRepository repository)
    {
        var start = CalendarDateBoundary.AtStartOfDay(date);
        var end = CalendarDateBoundary.AtStartOfDay(date.AddDays(1));
        var events = repository.GetEventsOverlapping(start, end)
            .OrderByDescending(IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.Start)
            .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
            .Select(CreateEvent)
            .ToArray();

        return new CalendarPeriodDayPresentation(
            date,
            date.ToString("ddd d"),
            date == today,
            date == selectedDate,
            events);
    }

    private static CalendarPeriodEventPresentation CreateEvent(CalendarEvent calendarEvent)
    {
        var isAllDay = IsAllDay(calendarEvent);
        return new CalendarPeriodEventPresentation(
            calendarEvent.Id,
            calendarEvent.Title,
            isAllDay ? "All day" : $"{calendarEvent.Start:HH:mm}–{calendarEvent.End:HH:mm}",
            calendarEvent.Location,
            calendarEvent.Start,
            calendarEvent.End,
            isAllDay,
            GetColorRole(calendarEvent.Id));
    }

    private static bool IsAllDay(CalendarEvent calendarEvent) =>
        calendarEvent.Start.TimeOfDay == TimeSpan.Zero &&
        calendarEvent.End.TimeOfDay == TimeSpan.Zero &&
        calendarEvent.Duration >= TimeSpan.FromDays(1);

    private static CalendarEventColorRole GetColorRole(string id)
    {
        var checksum = id.Aggregate(17, (value, character) => unchecked((value * 31) + character));
        return (CalendarEventColorRole)((checksum & int.MaxValue) % 4);
    }
}
