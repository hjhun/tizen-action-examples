namespace Calendar.App;

internal static class CalendarPeriodRenderPolicy
{
    internal const int WeekEventsPerDay = 5;
    internal const int DayEvents = 7;
    internal const int AgendaDays = 8;
    internal const int AgendaEventsPerDay = 4;

    internal static IReadOnlyList<string> GetRenderedEventIds(
        CalendarPeriodPresentation presentation,
        float agendaHeight = float.PositiveInfinity) =>
        presentation.ViewMode switch
        {
            CalendarViewMode.Week => presentation.Days
                .SelectMany(day => day.Events.Take(WeekEventsPerDay))
                .Select(item => item.EventId)
                .ToArray(),
            CalendarViewMode.Day => presentation.Days
                .Take(1)
                .SelectMany(day => day.Events.Take(DayEvents))
                .Select(item => item.EventId)
                .ToArray(),
            CalendarViewMode.Agenda => presentation.Days
                .Take(GetAgendaDayCount(presentation.Days.Count, agendaHeight))
                .SelectMany(day => day.Events.Take(AgendaEventsPerDay))
                .Select(item => item.EventId)
                .ToArray(),
            _ => Array.Empty<string>(),
        };

    internal static int GetAgendaDayCount(int availableDays, float height)
    {
        var limit = Math.Min(AgendaDays, Math.Max(0, availableDays));
        if (float.IsPositiveInfinity(height))
        {
            return limit;
        }

        var rendered = 0;
        var top = 0.0f;
        while (rendered < limit)
        {
            rendered++;
            top += 82.0f;
            if (top + 72.0f > height)
            {
                break;
            }
        }

        return rendered;
    }
}
