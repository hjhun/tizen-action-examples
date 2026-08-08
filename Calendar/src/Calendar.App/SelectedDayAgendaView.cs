using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class SelectedDayAgendaView
{
    public static View Create(
        CalendarAgendaPresentation agenda,
        CalendarUiState state,
        CalendarTheme theme,
        Position position,
        Size size,
        Action<CalendarUiCommand>? dispatch = null,
        Action? addEvent = null,
        Action? openReminders = null)
    {
        var pane = new View
        {
            Name = "SelectedDayAgenda",
            Position = position,
            Size = size,
            BackgroundColor = new Color(theme.SecondarySurface),
            CornerRadius = 18.0f,
        };

        pane.Add(CalendarDateCellView.CreateLabel(
            agenda.Date.Day.ToString(),
            theme.TextPrimary,
            pointSize: 10.0f,
            new Position(34.0f, 24.0f),
            new Size(110.0f, 74.0f),
            HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            agenda.Date.ToString("ddd").ToUpperInvariant(),
            theme.TextPrimary,
            pointSize: 4.0f,
            new Position(125.0f, 41.0f),
            new Size(110.0f, 42.0f),
            HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            agenda.Date.ToString("MMMM yyyy"),
            theme.TextSecondary,
            pointSize: 3.5f,
            new Position(35.0f, 97.0f),
            new Size(size.Width - 70.0f, 38.0f),
            HorizontalAlignment.Begin));

        if (agenda.IsEmpty)
        {
            pane.Add(CreateEmptyState(state, theme, size));
        }
        else
        {
            AddEventCards(pane, agenda, state, theme, size, dispatch);
        }

        pane.Add(CreateFooterAction("AddEvent", "Add event     +", CalendarFocusRegion.AgendaAdd, state, theme, size, size.Height - 142.0f, addEvent));
        pane.Add(CreateFooterAction("OpenReminders", "Reminders     ›", CalendarFocusRegion.AgendaReminders, state, theme, size, size.Height - 76.0f, openReminders));
        return pane;
    }

    private static void AddEventCards(
        View pane,
        CalendarAgendaPresentation agenda,
        CalendarUiState state,
        CalendarTheme theme,
        Size paneSize,
        Action<CalendarUiCommand>? dispatch)
    {
        const float cardTop = 150.0f;
        const float cardHeight = 126.0f;
        const float cardGap = 16.0f;
        var maxVisible = Math.Max(1, (int)((paneSize.Height - cardTop - 180.0f) / (cardHeight + cardGap)));
        var focusedIndex = state.FocusedAgendaIndex ?? 0;
        var startIndex = Math.Clamp(focusedIndex - maxVisible + 1, 0, Math.Max(0, agenda.Events.Count - maxVisible));

        foreach (var indexedEvent in agenda.Events
                     .Select((item, index) => (Item: item, Index: index))
                     .Skip(startIndex)
                     .Take(maxVisible))
        {
            var isFocused = state.FocusRegion == CalendarFocusRegion.AgendaEvents &&
                            indexedEvent.Index == focusedIndex;
            var top = cardTop + ((indexedEvent.Index - startIndex) * (cardHeight + cardGap));
            pane.Add(CreateEventCard(indexedEvent.Item, indexedEvent.Index, isFocused, theme, paneSize.Width, top, dispatch));
        }

        if (agenda.Events.Count > maxVisible)
        {
            pane.Add(CalendarDateCellView.CreateLabel(
                $"{focusedIndex + 1} / {agenda.Events.Count}",
                theme.TextSecondary,
                pointSize: 3.0f,
                new Position(paneSize.Width - 125.0f, paneSize.Height - 94.0f),
                new Size(90.0f, 30.0f),
                HorizontalAlignment.End));
        }
    }

    private static View CreateEventCard(
        CalendarAgendaEventPresentation agendaEvent,
        int index,
        bool isFocused,
        CalendarTheme theme,
        float paneWidth,
        float top,
        Action<CalendarUiCommand>? dispatch)
    {
        var card = new View
        {
            Name = $"CalendarEvent-{agendaEvent.EventId}",
            AccessibilityName = $"{agendaEvent.Title}, {agendaEvent.TimeText}{(string.IsNullOrWhiteSpace(agendaEvent.Location) ? string.Empty : $", {agendaEvent.Location}")}",
            Focusable = true,
            Position = new Position(30.0f, top),
            Size = new Size(paneWidth - 60.0f, 126.0f),
            BackgroundColor = new Color(theme.EventColors[(int)agendaEvent.ColorRole]),
            CornerRadius = 16.0f,
            BorderlineWidth = isFocused ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
            Scale = isFocused
                ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f)
                : Vector3.One,
        };

        card.Add(CalendarDateCellView.CreateLabel(
            agendaEvent.TimeText,
            theme.TextSecondary,
            pointSize: 3.4f,
            new Position(18.0f, 10.0f),
            new Size(110.0f, 30.0f),
            HorizontalAlignment.Begin));
        card.Add(CalendarDateCellView.CreateLabel(
            agendaEvent.Title,
            theme.TextPrimary,
            pointSize: 4.6f,
            new Position(18.0f, 40.0f),
            new Size(paneWidth - 100.0f, 43.0f),
            HorizontalAlignment.Begin));
        if (!string.IsNullOrWhiteSpace(agendaEvent.Location))
        {
            card.Add(CalendarDateCellView.CreateLabel(
                agendaEvent.Location,
                theme.TextSecondary,
                pointSize: 3.1f,
                new Position(18.0f, 83.0f),
                new Size(paneWidth - 100.0f, 30.0f),
                HorizontalAlignment.Begin));
        }

        if (dispatch is not null)
        {
            CalendarTouchBinder.Bind(card, () => dispatch(new CalendarUiCommand.SelectAgendaEvent(index)));
        }

        return card;
    }

    private static View CreateEmptyState(CalendarUiState state, CalendarTheme theme, Size paneSize)
    {
        var isFocused = state.FocusRegion == CalendarFocusRegion.AgendaEmptyState;
        var card = new View
        {
            Name = "AgendaEmptyState",
            AccessibilityName = "No events",
            Focusable = true,
            Position = new Position(30.0f, 160.0f),
            Size = new Size(paneSize.Width - 60.0f, 150.0f),
            BackgroundColor = new Color(theme.CellSurface),
            CornerRadius = 16.0f,
            BorderlineWidth = isFocused ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
            Scale = isFocused
                ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f)
                : Vector3.One,
        };
        card.Add(CalendarDateCellView.CreateLabel(
            "No events",
            theme.TextPrimary,
            pointSize: 5.0f,
            new Position(20.0f, 28.0f),
            new Size(paneSize.Width - 100.0f, 45.0f),
            HorizontalAlignment.Center));
        card.Add(CalendarDateCellView.CreateLabel(
            "Press Back to return to the month",
            theme.TextSecondary,
            pointSize: 3.1f,
            new Position(20.0f, 79.0f),
            new Size(paneSize.Width - 100.0f, 36.0f),
            HorizontalAlignment.Center));
        return card;
    }

    private static View CreateFooterAction(
        string name,
        string label,
        CalendarFocusRegion focusRegion,
        CalendarUiState state,
        CalendarTheme theme,
        Size paneSize,
        float top,
        Action? activate)
    {
        var isFocused = state.FocusRegion == focusRegion;
        var add = new View
        {
            Name = name,
            AccessibilityName = label,
            Focusable = true,
            Position = new Position(30.0f, top),
            Size = new Size(paneSize.Width - 60.0f, 52.0f),
            BackgroundColor = new Color(isFocused ? theme.CellSelectedSurface : theme.CellSurface),
            CornerRadius = 18.0f,
            BorderlineWidth = isFocused ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
            Scale = isFocused
                ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f)
                : Vector3.One,
        };
        add.Add(CalendarDateCellView.CreateLabel(
            label,
            theme.TextPrimary,
            pointSize: 3.5f,
            new Position(18.0f, 0.0f),
            new Size(paneSize.Width - 96.0f, 52.0f),
            HorizontalAlignment.Center));

        if (activate is not null)
        {
            CalendarTouchBinder.Bind(add, activate);
        }

        return add;
    }
}
