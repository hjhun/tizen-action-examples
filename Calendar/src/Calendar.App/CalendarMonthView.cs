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
        var insets = Window.Default.GetInsets();
        var viewport = ProportionalViewport.Create(
            windowSize.Width,
            windowSize.Height,
            insets.Start,
            insets.Top,
            insets.End,
            insets.Bottom);
        var safeX = (float)theme.SafeInsetHorizontal;
        var safeY = (float)theme.SafeInsetVertical;
        var contentWidth = ProportionalViewport.ReferenceWidth - (theme.SafeInsetHorizontal * 2.0f);
        var commandBarHeight = CalendarLayoutMetrics.CommandBarHeight;
        var contentTop = safeY + commandBarHeight + CalendarLayoutMetrics.CommandBarGap;
        var mainHeight = CalendarLayoutMetrics.CalculateMainHeight(theme);
        var paneGap = 24.0f;
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
        var canvas = new View
        {
            Name = "CalendarDesignCanvas",
            Position = new Position(viewport.OffsetX, viewport.OffsetY),
            Size = new Size(ProportionalViewport.ReferenceWidth, ProportionalViewport.ReferenceHeight),
            Scale = new Vector3(viewport.Scale, viewport.Scale, 1.0f),
            ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color(theme.RootSurface),
            FocusableChildren = true,
        };
        root.Add(canvas);

        var commandBar = CalendarCommandBarView.Create(
            state,
            theme,
            today,
            new Position(safeX, safeY),
            new Size(contentWidth, commandBarHeight),
            dispatch,
            openSearch,
            out var commandBarFocus);
        canvas.Add(commandBar);

        View? contentFocus;
        if (state.ViewMode == CalendarViewMode.Month)
        {
            contentFocus = AddMonthGrid(canvas, presentation, state, theme, safeX, contentTop, monthWidth, mainHeight, dispatch);
            canvas.Add(SelectedDayAgendaView.Create(
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
                        canvas.FindChildByName($"CalendarEvent-{presentation.Agenda.Events[index].EventId}"),
                    CalendarFocusRegion.AgendaEmptyState => canvas.FindChildByName("AgendaEmptyState"),
                    CalendarFocusRegion.AgendaAdd => canvas.FindChildByName("AddEvent"),
                    CalendarFocusRegion.AgendaReminders => canvas.FindChildByName("OpenReminders"),
                    _ => null,
                };
            }
        }
        else
        {
            var periodPresentation = CalendarPeriodPresentation.Create(state, repository, today);
            canvas.Add(CalendarPeriodView.Create(
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

    private static View? AddMonthGrid(
        View root,
        CalendarMonthPresentation presentation,
        CalendarUiState state,
        CalendarTheme theme,
        float left,
        float top,
        float monthWidth,
        float contentHeight,
        Action<CalendarUiCommand> dispatch)
    {
        const float weekdayHeight = 48.0f;
        const float rowGap = 8.0f;
        const float columnGap = 8.0f;
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
                pointSize: 3.1f,
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
