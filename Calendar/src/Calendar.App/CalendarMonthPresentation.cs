using Calendar.Domain;

namespace Calendar.App;

public enum CalendarEventColorRole
{
    Green,
    Blue,
    Orange,
    Purple,
}

public sealed record CalendarEventChipPresentation(
    string EventId,
    string Title,
    CalendarEventColorRole ColorRole);

public sealed record CalendarDateCellPresentation(
    DateOnly Date,
    bool IsInVisibleMonth,
    bool IsSunday,
    bool IsToday,
    bool IsSelected,
    bool IsFocused,
    IReadOnlyList<CalendarEventChipPresentation> EventChips,
    int OverflowCount);

public sealed record CalendarAgendaEventPresentation(
    string EventId,
    string TimeText,
    string Title,
    string Location,
    bool IsAllDay,
    CalendarEventColorRole ColorRole);

public sealed record CalendarAgendaPresentation(
    DateOnly Date,
    IReadOnlyList<CalendarAgendaEventPresentation> Events,
    string EmptyStateText)
{
    public bool IsEmpty => Events.Count == 0;
}

public sealed record CalendarMonthPresentation(
    IReadOnlyList<CalendarDateCellPresentation> Cells,
    CalendarAgendaPresentation Agenda)
{
    public static CalendarMonthPresentation Create(
        CalendarUiState state,
        CalendarEventRepository repository,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(repository);

        var cells = state.BuildMonthCells()
            .Select(cell => CreateCell(cell, state, repository, today))
            .ToArray();
        var agenda = CreateAgenda(state.SelectedDate, repository);
        return new CalendarMonthPresentation(cells, agenda);
    }

    private static CalendarDateCellPresentation CreateCell(
        CalendarMonthCell cell,
        CalendarUiState state,
        CalendarEventRepository repository,
        DateOnly today)
    {
        var events = GetEventsForDay(cell.Date, repository);
        var chips = events
            .Take(2)
            .Select(calendarEvent => new CalendarEventChipPresentation(
                calendarEvent.Id,
                calendarEvent.Title,
                GetColorRole(calendarEvent.Id)))
            .ToArray();

        return new CalendarDateCellPresentation(
            cell.Date,
            cell.IsInVisibleMonth,
            cell.Date.DayOfWeek == DayOfWeek.Sunday,
            cell.Date == today,
            cell.Date == state.SelectedDate,
            cell.Date == state.SelectedDate && state.FocusRegion == CalendarFocusRegion.MonthGrid,
            chips,
            Math.Max(0, events.Count - chips.Length));
    }

    private static CalendarAgendaPresentation CreateAgenda(
        DateOnly date,
        CalendarEventRepository repository)
    {
        var events = GetEventsForDay(date, repository)
            .OrderByDescending(IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.Start)
            .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
            .Select(calendarEvent =>
            {
                var isAllDay = IsAllDay(calendarEvent);
                return new CalendarAgendaEventPresentation(
                    calendarEvent.Id,
                    isAllDay ? "All day" : calendarEvent.Start.ToString("HH:mm"),
                    calendarEvent.Title,
                    calendarEvent.Location,
                    isAllDay,
                    GetColorRole(calendarEvent.Id));
            })
            .ToArray();

        return new CalendarAgendaPresentation(date, events, "No events");
    }

    private static IReadOnlyList<CalendarEvent> GetEventsForDay(
        DateOnly date,
        CalendarEventRepository repository)
    {
        var start = CalendarDateBoundary.AtStartOfDay(date);
        var end = CalendarDateBoundary.AtStartOfDay(date.AddDays(1));
        return repository.GetEventsOverlapping(start, end);
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
