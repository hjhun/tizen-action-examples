namespace Calendar.App;

// Also used by host tests: page identity and saved selection are independent of NUI actors.
internal sealed record CalendarAnnotationPage(string Id, string Surface, object State)
{
    internal static CalendarAnnotationPage Create(CalendarInteractionState state)
    {
        var surface = state.Surface == CalendarSurface.Calendar ? state.Calendar.ViewMode.ToString() : state.Surface.ToString();
        if (state.Surface == CalendarSurface.EventEditor) surface += state.EventEditor!.IsEditing ? "-edit" : "-new";
        if (state.Surface == CalendarSurface.ReminderEditor) surface += state.ReminderEditor!.IsEditing ? "-edit" : "-new";

        var page = new
        {
            surface,
            selectedDate = state.Calendar.SelectedDate.ToString("yyyy-MM-dd"),
            visibleMonth = state.Surface == CalendarSurface.Calendar ? state.Calendar.VisibleMonth.ToString("yyyy-MM") : null,
            selectedEventId = state.Surface is CalendarSurface.EventDetail or CalendarSurface.EventEditor or CalendarSurface.DeleteEventConfirmation ? state.SelectedEventId : null,
            selectedReminderId = state.Surface is CalendarSurface.ReminderEditor or CalendarSurface.DeleteReminderConfirmation ? state.SelectedReminderId : null,
            search = state.Surface == CalendarSurface.Search && state.Search is { } search ? new
            {
                search.Keyword, search.StartDate, search.EndDateExclusive, search.HasApplied,
                search.SearchTitle, search.SearchLocation, search.SearchNote, resultCount = search.ResultEventIds.Count,
            } : null,
        };
        return new($"calendar:page:{surface}", surface, page);
    }
}
