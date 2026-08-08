namespace Calendar.App;

public abstract record CalendarUiCommand
{
    public sealed record SelectDate(DateOnly Date) : CalendarUiCommand;
    public sealed record SelectAgendaEvent(int Index) : CalendarUiCommand;
    public sealed record ActivateToday : CalendarUiCommand;
    public sealed record ShowPreviousPeriod : CalendarUiCommand;
    public sealed record ShowNextPeriod : CalendarUiCommand;
    public sealed record ChangeViewMode(CalendarViewMode ViewMode) : CalendarUiCommand;
    public sealed record OpenEvent(string EventId) : CalendarUiCommand;
}

public sealed class CalendarTouchActivation
{
    private bool _isPressed;

    public void PointerDown() => _isPressed = true;

    public bool PointerUp(bool isInside)
    {
        var shouldActivate = _isPressed && isInside;
        _isPressed = false;
        return shouldActivate;
    }
}

public static class CalendarUiReducer
{
    public static CalendarUiState Reduce(
        CalendarUiState state,
        CalendarUiCommand command,
        DateOnly today,
        int selectedDateEventCount) => command switch
    {
        CalendarUiCommand.SelectDate selectDate => state with
        {
            SelectedDate = selectDate.Date,
            VisibleMonth = new DateOnly(selectDate.Date.Year, selectDate.Date.Month, 1),
            IsAgendaOpen = false,
            FocusRegion = CalendarFocusRegion.MonthGrid,
            FocusedAgendaIndex = null,
        },
        CalendarUiCommand.SelectAgendaEvent selectAgendaEvent when
            selectedDateEventCount > 0 &&
            selectAgendaEvent.Index >= 0 &&
            selectAgendaEvent.Index < selectedDateEventCount => state.EnterAgenda(selectedDateEventCount) with
        {
            FocusedAgendaIndex = selectAgendaEvent.Index,
        },
        CalendarUiCommand.ActivateToday => state.ActivateToday(today).FocusHeader(CalendarFocusRegion.Today),
        CalendarUiCommand.ShowPreviousPeriod => state.FocusHeader(CalendarFocusRegion.PreviousPeriod).MovePeriod(-1),
        CalendarUiCommand.ShowNextPeriod => state.FocusHeader(CalendarFocusRegion.NextPeriod).MovePeriod(1),
        CalendarUiCommand.ChangeViewMode changeViewMode => state.ChangeViewMode(changeViewMode.ViewMode),
        CalendarUiCommand.OpenEvent openEvent => state.ViewMode == CalendarViewMode.Month
            ? state with { FocusedEventId = openEvent.EventId }
            : state.FocusPeriodEvent(openEvent.EventId),
        _ => state,
    };
}
