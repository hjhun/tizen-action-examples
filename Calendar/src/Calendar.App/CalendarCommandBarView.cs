using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class CalendarCommandBarView
{
    public static View Create(
        CalendarUiState state,
        CalendarTheme theme,
        DateOnly today,
        Position position,
        Size size,
        Action<CalendarUiCommand> dispatch,
        Action openSearch,
        out View? preferredFocus)
    {
        var bar = new View
        {
            Name = "CalendarCommandBar",
            Position = position,
            Size = size,
            BackgroundColor = new Color(theme.SecondarySurface),
            CornerRadius = 18.0f,
            FocusableChildren = true,
        };

        preferredFocus = null;
        var scale = size.Height / 82.0f;
        var x = 18.0f * scale;
        AddControl(bar, CreateControl("PreviousPeriod", "Prev", "Previous period", CalendarFocusRegion.PreviousPeriod, state, theme,
            new Position(x, 12.0f * scale), new Size(82.0f * scale, 58.0f * scale),
            () => dispatch(new CalendarUiCommand.ShowPreviousPeriod())), state, ref preferredFocus);
        x += 92.0f * scale;
        AddControl(bar, CreateControl("TodayControl", "Today", $"Today, {today:dddd, MMMM d}", CalendarFocusRegion.Today, state, theme,
            new Position(x, 12.0f * scale), new Size(102.0f * scale, 58.0f * scale),
            () => dispatch(new CalendarUiCommand.ActivateToday())), state, ref preferredFocus);
        x += 112.0f * scale;
        AddControl(bar, CreateControl("NextPeriod", "Next", "Next period", CalendarFocusRegion.NextPeriod, state, theme,
            new Position(x, 12.0f * scale), new Size(82.0f * scale, 58.0f * scale),
            () => dispatch(new CalendarUiCommand.ShowNextPeriod())), state, ref preferredFocus);
        x += 106.0f * scale;

        var title = state.ViewMode switch
        {
            CalendarViewMode.Week => FormatWeekTitle(state.SelectedDate),
            CalendarViewMode.Day => state.SelectedDate.ToString("MMM d, yyyy"),
            CalendarViewMode.Agenda => $"{state.VisibleMonth:MMM yyyy} agenda",
            _ => state.VisibleMonth.ToString("MMMM yyyy"),
        };
        bar.Add(CalendarDateCellView.CreateLabel(
            title,
            theme.TextPrimary,
            5.0f * scale,
            new Position(x, 0.0f),
            new Size(330.0f * scale, size.Height),
            HorizontalAlignment.Begin));
        x += 340.0f * scale;

        var modes = new[]
        {
            (CalendarViewMode.Month, CalendarFocusRegion.MonthMode, "Month"),
            (CalendarViewMode.Week, CalendarFocusRegion.WeekMode, "Week"),
            (CalendarViewMode.Day, CalendarFocusRegion.DayMode, "Day"),
            (CalendarViewMode.Agenda, CalendarFocusRegion.AgendaMode, "Agenda"),
        };
        foreach (var (mode, region, label) in modes)
        {
            var selected = state.ViewMode == mode;
            var control = CreateControl($"{label}Mode", label, $"{label} view{(selected ? ", selected" : string.Empty)}", region, state, theme,
                new Position(x, 12.0f * scale), new Size(112.0f * scale, 58.0f * scale),
                () => dispatch(new CalendarUiCommand.ChangeViewMode(mode)),
                selected);
            AddControl(bar, control, state, ref preferredFocus);
            x += 122.0f * scale;
        }

        x += 12.0f * scale;
        AddControl(bar, CreateControl("SearchControl", "Search", "Search calendar events", CalendarFocusRegion.Search, state, theme,
            new Position(x, 12.0f * scale), new Size(150.0f * scale, 58.0f * scale), openSearch), state, ref preferredFocus);

        return bar;
    }

    private static string FormatWeekTitle(DateOnly selectedDate)
    {
        var start = selectedDate.AddDays(-(int)selectedDate.DayOfWeek);
        return $"{start:MMM d} – {start.AddDays(6):MMM d, yyyy}";
    }

    private static View CreateControl(
        string name,
        string label,
        string accessibilityName,
        CalendarFocusRegion region,
        CalendarUiState state,
        CalendarTheme theme,
        Position position,
        Size size,
        Action activate,
        bool selected = false)
    {
        var focused = state.FocusRegion == region;
        var control = new View
        {
            Name = name,
            AccessibilityName = accessibilityName,
            Focusable = true,
            Position = position,
            Size = size,
            BackgroundColor = new Color(selected || focused ? theme.CellSelectedSurface : theme.CellSurface),
            CornerRadius = 14.0f,
            BorderlineWidth = focused ? theme.FocusOutlineWidth : (selected ? 2.0f : 0.0f),
            BorderlineColor = new Color(focused ? theme.FocusOutline : theme.TextSecondary),
            Scale = focused ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f) : Vector3.One,
        };
        control.Add(CalendarDateCellView.CreateLabel(label, theme.TextPrimary, 3.5f, new Position(8.0f, 0.0f), new Size(size.Width - 16.0f, size.Height), HorizontalAlignment.Center));
        CalendarTouchBinder.Bind(control, activate);
        return control;
    }

    private static void AddControl(View bar, View control, CalendarUiState state, ref View? preferredFocus)
    {
        bar.Add(control);
        if ((control.Name, state.FocusRegion) is
            ("PreviousPeriod", CalendarFocusRegion.PreviousPeriod) or
            ("TodayControl", CalendarFocusRegion.Today) or
            ("NextPeriod", CalendarFocusRegion.NextPeriod) or
            ("SearchControl", CalendarFocusRegion.Search) or
            ("MonthMode", CalendarFocusRegion.MonthMode) or
            ("WeekMode", CalendarFocusRegion.WeekMode) or
            ("DayMode", CalendarFocusRegion.DayMode) or
            ("AgendaMode", CalendarFocusRegion.AgendaMode))
        {
            preferredFocus = control;
        }
    }
}
