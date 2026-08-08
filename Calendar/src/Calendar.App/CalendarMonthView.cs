using Calendar.Domain;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class CalendarMonthView
{
    public static View Create(
        CalendarUiState state,
        CalendarEventRepository repository,
        DateOnly today,
        Action<CalendarUiCommand> dispatch,
        Action addEvent,
        Action openReminders,
        Action openSearch,
        out View? preferredFocus)
    {
        var theme = CalendarTheme.Light;
        var presentation = CalendarMonthPresentation.Create(state, repository, today);
        var windowSize = Window.Default.WindowSize;
        var scaleX = windowSize.Width / 1920.0f;
        var scaleY = windowSize.Height / 1080.0f;
        var scale = Math.Min(scaleX, scaleY);
        var safeX = theme.SafeInsetHorizontal * scaleX;
        var safeY = theme.SafeInsetVertical * scaleY;
        var contentWidth = windowSize.Width - (safeX * 2.0f);
        var commandBarHeight = 82.0f * scale;
        var contentTop = safeY + commandBarHeight + (18.0f * scale);
        var mainHeight = CalculateMainHeight(windowSize.Width, windowSize.Height, theme);
        var paneGap = 24.0f * scale;
        var monthWidth = (contentWidth - paneGap) * theme.MonthPaneRatio;
        var agendaWidth = contentWidth - paneGap - monthWidth;

        var root = new View
        {
            Name = "CalendarOneUiSplitRoot",
            WidthResizePolicy = ResizePolicyType.FillToParent,
            HeightResizePolicy = ResizePolicyType.FillToParent,
            BackgroundColor = new Color(theme.RootSurface),
            FocusableChildren = true,
        };

        var commandBar = CalendarCommandBarView.Create(
            state,
            theme,
            today,
            new Position(safeX, safeY),
            new Size(contentWidth, commandBarHeight),
            dispatch,
            openSearch,
            out var commandBarFocus);
        root.Add(commandBar);

        View? contentFocus;
        if (state.ViewMode == CalendarViewMode.Month)
        {
            contentFocus = AddMonthGrid(root, presentation, state, theme, safeX, contentTop, monthWidth, mainHeight, scale, dispatch);
            root.Add(SelectedDayAgendaView.Create(
                presentation.Agenda,
                state,
                theme,
                new Position(safeX + monthWidth + paneGap, contentTop),
                new Size(agendaWidth, mainHeight),
                dispatch,
                addEvent,
                openReminders));

            if (contentFocus is null)
            {
                contentFocus = state.FocusRegion switch
                {
                    CalendarFocusRegion.AgendaEvents when state.FocusedAgendaIndex is int index && index >= 0 && index < presentation.Agenda.Events.Count =>
                        root.FindChildByName($"CalendarEvent-{presentation.Agenda.Events[index].EventId}"),
                    CalendarFocusRegion.AgendaEmptyState => root.FindChildByName("AgendaEmptyState"),
                    CalendarFocusRegion.AgendaAdd => root.FindChildByName("AddEvent"),
                    CalendarFocusRegion.AgendaReminders => root.FindChildByName("OpenReminders"),
                    _ => null,
                };
            }
        }
        else
        {
            var periodPresentation = CalendarPeriodPresentation.Create(state, repository, today);
            root.Add(CalendarPeriodView.Create(
                periodPresentation,
                state,
                theme,
                new Position(safeX, contentTop),
                new Size(contentWidth, mainHeight),
                dispatch,
                out contentFocus));
        }

        preferredFocus = commandBarFocus ?? contentFocus;
        return root;
    }

    internal static float CalculateMainHeight(float windowWidth, float windowHeight, CalendarTheme theme)
    {
        var scale = Math.Min(windowWidth / 1920.0f, windowHeight / 1080.0f);
        var safeY = theme.SafeInsetVertical * (windowHeight / 1080.0f);
        var contentHeight = windowHeight - (safeY * 2.0f);
        return contentHeight - (82.0f * scale) - (18.0f * scale);
    }

    private static View? AddMonthGrid(
        View root,
        CalendarMonthPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        float left,
        float top,
        float monthWidth,
        float contentHeight,
        float scale,
        Action<CalendarUiCommand> dispatch)
    {
        var weekdayHeight = 48.0f * scale;
        var rowGap = 8.0f * scale;
        var columnGap = 8.0f * scale;
        var gridTop = top + weekdayHeight;
        var gridHeight = contentHeight - weekdayHeight;
        var cellWidth = (monthWidth - (columnGap * 6.0f)) / 7.0f;
        var cellHeight = (gridHeight - (rowGap * 5.0f)) / 6.0f;
        var weekdayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        for (var column = 0; column < weekdayNames.Length; column++)
        {
            root.Add(CalendarDateCellView.CreateLabel(
                weekdayNames[column],
                column == 0 ? theme.SundayAccent : theme.TextSecondary,
                pointSize: 3.1f * scale,
                new Position(left + (column * (cellWidth + columnGap)), top),
                new Size(cellWidth, weekdayHeight),
                HorizontalAlignment.Center));
        }

        View? preferredFocus = null;
        for (var index = 0; index < presentation.Cells.Count; index++)
        {
            var row = index / 7;
            var column = index % 7;
            var cell = CalendarDateCellView.Create(
                presentation.Cells[index],
                theme,
                new Position(
                    left + (column * (cellWidth + columnGap)),
                    gridTop + (row * (cellHeight + rowGap))),
                new Size(cellWidth, cellHeight),
                date => dispatch(new CalendarUiCommand.SelectDate(date)),
                eventId => dispatch(new CalendarUiCommand.OpenEvent(eventId)),
                state.FocusedEventId);
            cell.Focusable = true;
            cell.AccessibilityName = $"{presentation.Cells[index].Date:dddd, MMMM d}, {presentation.Cells[index].EventChips.Count + presentation.Cells[index].OverflowCount} events";
            root.Add(cell);
            if (presentation.Cells[index].Date == state.SelectedDate && state.FocusRegion == CalendarFocusRegion.MonthGrid)
            {
                preferredFocus = cell;
            }
        }

        return preferredFocus;
    }
}
