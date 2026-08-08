using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal static class CalendarDateCellView
{
    public static View Create(
        CalendarDateCellPresentation cell,
        CalendarTheme theme,
        Position position,
        Size size,
        Action<DateOnly>? activate = null,
        Action<string>? openEvent = null,
        string? focusedEventId = null)
    {
        var surface = new View
        {
            Name = $"CalendarDateCell-{cell.Date:yyyy-MM-dd}",
            Position = position,
            Size = size,
            BackgroundColor = new Color(cell.IsSelected
                ? theme.CellSelectedSurface
                : cell.IsInVisibleMonth
                    ? theme.CellSurface
                    : theme.CellOutOfMonthSurface),
            CornerRadius = 10.0f,
            BorderlineWidth = cell.IsFocused ? theme.FocusOutlineWidth : 0.0f,
            BorderlineColor = new Color(theme.FocusOutline),
            Scale = cell.IsFocused
                ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f)
                : Vector3.One,
        };

        var dateTextColor = !cell.IsInVisibleMonth
            ? theme.TextDisabled
            : cell.IsSunday
                ? theme.SundayAccent
                : theme.TextPrimary;

        if (cell.IsToday)
        {
            var pill = new View
            {
                Position = new Position(8.0f, 7.0f),
                Size = new Size(44.0f, 38.0f),
                BackgroundColor = new Color(theme.TodayPillSurface),
                CornerRadius = 12.0f,
            };
            pill.Add(CreateLabel(
                cell.Date.Day.ToString(),
                theme.TodayPillText,
                pointSize: 4.8f,
                new Position(0.0f, 0.0f),
                new Size(44.0f, 38.0f),
                HorizontalAlignment.Center));
            surface.Add(pill);
        }
        else
        {
            surface.Add(CreateLabel(
                cell.Date.Day.ToString(),
                dateTextColor,
                pointSize: 4.8f,
                new Position(10.0f, 6.0f),
                new Size(size.Width - 20.0f, 40.0f),
                HorizontalAlignment.Begin));
        }

        var chipTop = 53.0f;
        for (var index = 0; index < cell.EventChips.Count; index++)
        {
            var chip = cell.EventChips[index];
            var isFocused = string.Equals(chip.EventId, focusedEventId, StringComparison.Ordinal);
            var chipView = new View
            {
                Name = $"CalendarEvent-{chip.EventId}",
                AccessibilityName = chip.Title,
                Focusable = true,
                Position = new Position(7.0f, chipTop + (index * 32.0f)),
                Size = new Size(size.Width - 14.0f, 27.0f),
                BackgroundColor = new Color(theme.EventColors[(int)chip.ColorRole]),
                CornerRadius = 7.0f,
                BorderlineWidth = isFocused ? theme.FocusOutlineWidth : 0.0f,
                BorderlineColor = new Color(theme.FocusOutline),
                Scale = isFocused ? new Vector3(theme.FocusScale, theme.FocusScale, 1.0f) : Vector3.One,
            };
            chipView.Add(CreateLabel(
                chip.Title,
                theme.TextPrimary,
                pointSize: 3.0f,
                new Position(7.0f, 0.0f),
                new Size(size.Width - 28.0f, 27.0f),
                HorizontalAlignment.Begin));
            if (openEvent is not null)
            {
                CalendarTouchBinder.Bind(chipView, () => openEvent(chip.EventId));
            }
            surface.Add(chipView);
        }

        if (cell.OverflowCount > 0)
        {
            surface.Add(CreateLabel(
                $"+{cell.OverflowCount}",
                theme.TextSecondary,
                pointSize: 3.0f,
                new Position(10.0f, Math.Min(size.Height - 27.0f, chipTop + (cell.EventChips.Count * 32.0f))),
                new Size(size.Width - 20.0f, 24.0f),
                HorizontalAlignment.Begin));
        }

        if (activate is not null)
        {
            CalendarTouchBinder.Bind(surface, () => activate(cell.Date));
        }

        return surface;
    }

    internal static TextLabel CreateLabel(
        string text,
        string color,
        float pointSize,
        Position position,
        Size size,
        HorizontalAlignment alignment) => new(text)
    {
        Position = position,
        Size = size,
        TextColor = new Color(color),
        PointSize = pointSize,
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
        Ellipsis = true,
        MultiLine = false,
    };
}
