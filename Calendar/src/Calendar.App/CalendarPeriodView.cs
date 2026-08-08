using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class CalendarPeriodView
{

    public static View Create(
        CalendarPeriodPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        Position position,
        Size size,
        Action<CalendarUiCommand> dispatch,
        out View? preferredFocus)
    {
        var root = new View
        {
            Name = $"Calendar{presentation.ViewMode}View",
            AccessibilityName = $"{presentation.ViewMode} calendar view",
            Position = position,
            Size = size,
            BackgroundColor = new Color(theme.RootSurface),
            FocusableChildren = true,
        };
        preferredFocus = null;

        switch (presentation.ViewMode)
        {
            case CalendarViewMode.Week:
                AddWeek(root, presentation, state, theme, size, dispatch, ref preferredFocus);
                break;
            case CalendarViewMode.Day:
                AddDay(root, presentation, state, theme, size, dispatch, ref preferredFocus);
                break;
            case CalendarViewMode.Agenda:
                AddAgenda(root, presentation, state, theme, size, dispatch, ref preferredFocus);
                break;
        }

        return root;
    }

    private static void AddWeek(
        View root,
        CalendarPeriodPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        Size size,
        Action<CalendarUiCommand> dispatch,
        ref View? preferredFocus)
    {
        var gap = 12.0f;
        var columnWidth = (size.Width - (gap * 6.0f)) / 7.0f;
        for (var index = 0; index < presentation.Days.Count; index++)
        {
            var day = presentation.Days[index];
            var selected = day.Date == state.SelectedDate;
            var column = new View
            {
                Name = $"WeekDay-{day.Date:yyyy-MM-dd}",
                AccessibilityName = $"{day.Date:dddd, MMMM d}, {day.Events.Count} events",
                Focusable = true,
                FocusableChildren = true,
                Position = new Position(index * (columnWidth + gap), 0.0f),
                Size = new Size(columnWidth, size.Height),
                BackgroundColor = new Color(selected ? theme.CellSelectedSurface : theme.SecondarySurface),
                CornerRadius = 16.0f,
                BorderlineWidth = selected && state.FocusRegion == CalendarFocusRegion.MonthGrid ? theme.FocusOutlineWidth : 0.0f,
                BorderlineColor = new Color(theme.FocusOutline),
            };
            column.Add(CalendarDateCellView.CreateLabel(
                day.Date.ToString("dddd\nMMM d"),
                day.IsToday ? theme.SundayAccent : theme.TextPrimary,
                3.8f,
                new Position(12.0f, 12.0f),
                new Size(columnWidth - 24.0f, 78.0f),
                HorizontalAlignment.Center));
            CalendarTouchBinder.Bind(column, () => dispatch(new CalendarUiCommand.SelectDate(day.Date)));
            root.Add(column);
            if (selected && state.FocusRegion == CalendarFocusRegion.MonthGrid)
            {
                preferredFocus = column;
            }

            var maxEvents = Math.Min(CalendarPeriodRenderPolicy.WeekEventsPerDay, day.Events.Count);
            for (var eventIndex = 0; eventIndex < maxEvents; eventIndex++)
            {
                var card = CreateEventCard(
                    day.Events[eventIndex],
                    theme,
                    new Position(10.0f, 100.0f + (eventIndex * 100.0f)),
                    new Size(columnWidth - 20.0f, 84.0f),
                    dispatch,
                    state.FocusedEventId == day.Events[eventIndex].EventId);
                column.Add(card);
            }
            if (day.Events.Count > maxEvents)
            {
                column.Add(CalendarDateCellView.CreateLabel($"+{day.Events.Count - maxEvents} more", theme.TextSecondary, 3.0f,
                    new Position(14.0f, 100.0f + (maxEvents * 100.0f)), new Size(columnWidth - 28.0f, 42.0f), HorizontalAlignment.Begin));
            }
        }
    }

    private static void AddDay(
        View root,
        CalendarPeriodPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        Size size,
        Action<CalendarUiCommand> dispatch,
        ref View? preferredFocus)
    {
        var day = presentation.Days[0];
        var daySurface = new View
        {
            Name = $"DaySurface-{day.Date:yyyy-MM-dd}",
            AccessibilityName = $"{day.Date:dddd, MMMM d}, {day.Events.Count} events",
            Focusable = true,
            FocusableChildren = true,
            Size = size,
            BackgroundColor = new Color(theme.SecondarySurface),
            CornerRadius = 18.0f,
            BorderlineWidth = state.FocusRegion == CalendarFocusRegion.MonthGrid ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
        };
        root.Add(daySurface);
        if (state.FocusRegion == CalendarFocusRegion.MonthGrid)
        {
            preferredFocus = daySurface;
        }

        daySurface.Add(CalendarDateCellView.CreateLabel(day.Date.ToString("dddd, MMMM d"), theme.TextPrimary, 6.5f,
            new Position(36.0f, 22.0f), new Size(size.Width - 72.0f, 72.0f), HorizontalAlignment.Begin));
        if (day.Events.Count == 0)
        {
            daySurface.Add(CreateEmptyState("No events today", theme, new Position(36.0f, 150.0f), new Size(size.Width - 72.0f, 150.0f)));
            return;
        }

        for (var index = 0; index < Math.Min(CalendarPeriodRenderPolicy.DayEvents, day.Events.Count); index++)
        {
            var card = CreateEventCard(day.Events[index], theme,
                new Position(36.0f, 116.0f + (index * 105.0f)), new Size(size.Width - 72.0f, 88.0f), dispatch,
                state.FocusedEventId == day.Events[index].EventId);
            daySurface.Add(card);
        }
    }

    private static void AddAgenda(
        View root,
        CalendarPeriodPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        Size size,
        Action<CalendarUiCommand> dispatch,
        ref View? preferredFocus)
    {
        if (presentation.IsEmpty)
        {
            var empty = CreateEmptyState(presentation.EmptyStateText, theme, new Position(0.0f, 0.0f), size);
            empty.Name = "AgendaPeriodEmptyState";
            empty.AccessibilityName = presentation.EmptyStateText;
            empty.Focusable = true;
            root.Add(empty);
            preferredFocus = state.FocusRegion == CalendarFocusRegion.MonthGrid ? empty : null;
            return;
        }

        var top = 0.0f;
        foreach (var day in presentation.Days.Take(
                     CalendarPeriodRenderPolicy.GetAgendaDayCount(presentation.Days.Count, size.Height)))
        {
            root.Add(CalendarDateCellView.CreateLabel(day.Date.ToString("dddd, MMMM d"), theme.TextPrimary, 4.2f,
                new Position(12.0f, top), new Size(290.0f, 56.0f), HorizontalAlignment.Begin));
            var eventLeft = 320.0f;
            foreach (var calendarEvent in day.Events.Take(CalendarPeriodRenderPolicy.AgendaEventsPerDay))
            {
                var width = Math.Min(700.0f, (size.Width - eventLeft - 20.0f) / Math.Max(1, Math.Min(CalendarPeriodRenderPolicy.AgendaEventsPerDay, day.Events.Count)) - 10.0f);
                root.Add(CreateEventCard(calendarEvent, theme, new Position(eventLeft, top), new Size(width, 64.0f), dispatch,
                    state.FocusedEventId == calendarEvent.EventId));
                eventLeft += width + 10.0f;
            }
            top += 82.0f;
        }
    }

    private static View CreateEventCard(
        CalendarPeriodEventPresentation calendarEvent,
        CalendarTheme theme,
        Position position,
        Size size,
        Action<CalendarUiCommand> dispatch,
        bool focused)
    {
        var card = new View
        {
            Name = $"CalendarEvent-{calendarEvent.EventId}",
            AccessibilityName = $"{calendarEvent.Title}, {calendarEvent.TimeText}{(string.IsNullOrWhiteSpace(calendarEvent.Location) ? string.Empty : $", {calendarEvent.Location}")}",
            Focusable = true,
            Position = position,
            Size = size,
            BackgroundColor = new Color(theme.EventColors[(int)calendarEvent.ColorRole]),
            CornerRadius = 14.0f,
            BorderlineWidth = focused ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
            Scale = focused ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f) : Vector3.One,
        };
        card.Add(CalendarDateCellView.CreateLabel(
            $"{calendarEvent.TimeText}  {calendarEvent.Title}{(string.IsNullOrWhiteSpace(calendarEvent.Location) ? string.Empty : $"  ·  {calendarEvent.Location}")}",
            theme.TextPrimary,
            3.0f,
            new Position(12.0f, 4.0f),
            new Size(size.Width - 24.0f, size.Height - 8.0f),
            HorizontalAlignment.Begin));
        CalendarTouchBinder.Bind(card, () => dispatch(new CalendarUiCommand.OpenEvent(calendarEvent.EventId)));
        return card;
    }

    private static View CreateEmptyState(string text, CalendarTheme theme, Position position, Size size)
    {
        var empty = new View
        {
            Position = position,
            Size = size,
            BackgroundColor = new Color(theme.CellSurface),
            CornerRadius = 18.0f,
        };
        empty.Add(CalendarDateCellView.CreateLabel(text, theme.TextSecondary, 5.0f,
            new Position(20.0f, 20.0f), new Size(size.Width - 40.0f, size.Height - 40.0f), HorizontalAlignment.Center));
        return empty;
    }
}
