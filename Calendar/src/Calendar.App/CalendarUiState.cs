namespace Calendar.App;

public enum CalendarBackResult
{
    CloseAgenda,
    ExitApplication,
}

public enum CalendarFocusRegion
{
    MonthGrid,
    AgendaEvents,
    AgendaEmptyState,
    AgendaAdd,
    AgendaReminders,
    PeriodEvents,
    PeriodEmptyState,
    Today,
    PreviousPeriod,
    NextPeriod,
    Search,
    MonthMode,
    WeekMode,
    DayMode,
    AgendaMode,
}

public enum CalendarViewMode
{
    Month,
    Week,
    Day,
    Agenda,
}

public sealed record CalendarMonthCell(DateOnly Date, bool IsInVisibleMonth);

public sealed record CalendarUiState(
    DateOnly VisibleMonth,
    DateOnly SelectedDate,
    bool IsAgendaOpen,
    CalendarFocusRegion FocusRegion,
    int? FocusedAgendaIndex)
{
    public CalendarViewMode ViewMode { get; init; } = CalendarViewMode.Month;

    public string? FocusedEventId { get; init; }

    public static CalendarUiState Create(DateOnly selectedDate) => new(
        new DateOnly(selectedDate.Year, selectedDate.Month, 1),
        selectedDate,
        IsAgendaOpen: false,
        CalendarFocusRegion.MonthGrid,
        FocusedAgendaIndex: null);

    public CalendarUiState MoveDays(int days)
    {
        var selectedDate = SelectedDate.AddDays(days);
        return this with
        {
            SelectedDate = selectedDate,
            VisibleMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1),
            IsAgendaOpen = false,
            FocusRegion = CalendarFocusRegion.MonthGrid,
            FocusedAgendaIndex = null,
            FocusedEventId = null,
        };
    }

    public CalendarUiState MovePeriod(int periods)
    {
        if (periods == 0)
        {
            return this;
        }

        DateOnly selectedDate;
        if (ViewMode is CalendarViewMode.Month or CalendarViewMode.Agenda)
        {
            var targetMonth = VisibleMonth.AddMonths(periods);
            var targetDay = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month));
            selectedDate = new DateOnly(targetMonth.Year, targetMonth.Month, targetDay);
        }
        else
        {
            selectedDate = SelectedDate.AddDays(ViewMode == CalendarViewMode.Week ? periods * 7 : periods);
        }

        var contentAnchor = ViewMode == CalendarViewMode.Month
            ? CalendarFocusRegion.MonthGrid
            : CalendarFocusRegion.PeriodEmptyState;
        return this with
        {
            SelectedDate = selectedDate,
            VisibleMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1),
            IsAgendaOpen = false,
            FocusRegion = IsHeaderFocused ? FocusRegion : contentAnchor,
            FocusedAgendaIndex = null,
            FocusedEventId = null,
        };
    }

    public CalendarUiState ChangeViewMode(CalendarViewMode viewMode) => this with
    {
        ViewMode = viewMode,
        VisibleMonth = new DateOnly(SelectedDate.Year, SelectedDate.Month, 1),
        IsAgendaOpen = false,
        FocusRegion = viewMode switch
        {
            CalendarViewMode.Month => CalendarFocusRegion.MonthMode,
            CalendarViewMode.Week => CalendarFocusRegion.WeekMode,
            CalendarViewMode.Day => CalendarFocusRegion.DayMode,
            CalendarViewMode.Agenda => CalendarFocusRegion.AgendaMode,
            _ => CalendarFocusRegion.MonthGrid,
        },
        FocusedAgendaIndex = null,
        FocusedEventId = null,
    };

    public CalendarUiState FocusHeader(CalendarFocusRegion focusRegion)
    {
        if (!IsHeaderRegion(focusRegion))
        {
            throw new ArgumentOutOfRangeException(nameof(focusRegion));
        }

        return this with
        {
            IsAgendaOpen = false,
            FocusRegion = focusRegion,
            FocusedAgendaIndex = null,
            FocusedEventId = null,
        };
    }

    public bool IsHeaderFocused => IsHeaderRegion(FocusRegion);

    public CalendarUiState MoveHeaderFocus(int delta)
    {
        if (!IsHeaderFocused || delta == 0)
        {
            return this;
        }

        var regions = new[]
        {
            CalendarFocusRegion.PreviousPeriod,
            CalendarFocusRegion.Today,
            CalendarFocusRegion.NextPeriod,
            CalendarFocusRegion.MonthMode,
            CalendarFocusRegion.WeekMode,
            CalendarFocusRegion.DayMode,
            CalendarFocusRegion.AgendaMode,
            CalendarFocusRegion.Search,
        };
        var current = Array.IndexOf(regions, FocusRegion);
        return FocusHeader(regions[Math.Clamp(current + delta, 0, regions.Length - 1)]);
    }

    private static bool IsHeaderRegion(CalendarFocusRegion focusRegion) => focusRegion is
        CalendarFocusRegion.PreviousPeriod or
        CalendarFocusRegion.Today or
        CalendarFocusRegion.NextPeriod or
        CalendarFocusRegion.Search or
        CalendarFocusRegion.MonthMode or
        CalendarFocusRegion.WeekMode or
        CalendarFocusRegion.DayMode or
        CalendarFocusRegion.AgendaMode;

    public CalendarUiState EnterAgenda(int eventCount)
    {
        if (eventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        }

        return this with
        {
            IsAgendaOpen = true,
            FocusRegion = eventCount == 0
                ? CalendarFocusRegion.AgendaEmptyState
                : CalendarFocusRegion.AgendaEvents,
            FocusedAgendaIndex = eventCount == 0 ? null : 0,
            FocusedEventId = null,
        };
    }

    public CalendarUiState MoveAgenda(int delta, int eventCount)
    {
        if (eventCount <= 0 || FocusRegion != CalendarFocusRegion.AgendaEvents)
        {
            return this;
        }

        var currentIndex = FocusedAgendaIndex ?? 0;
        return this with
        {
            FocusedAgendaIndex = Math.Clamp(currentIndex + delta, 0, eventCount - 1),
        };
    }

    public CalendarUiState MoveAgendaFocus(int delta, int eventCount)
    {
        if (eventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        }

        if (FocusRegion == CalendarFocusRegion.AgendaAdd)
        {
            if (delta >= 0)
            {
                return this with
                {
                    FocusRegion = CalendarFocusRegion.AgendaReminders,
                    FocusedAgendaIndex = null,
                };
            }

            return this with
            {
                FocusRegion = eventCount == 0
                    ? CalendarFocusRegion.AgendaEmptyState
                    : CalendarFocusRegion.AgendaEvents,
                FocusedAgendaIndex = eventCount == 0 ? null : eventCount - 1,
            };
        }

        if (FocusRegion == CalendarFocusRegion.AgendaReminders)
        {
            return delta < 0
                ? this with { FocusRegion = CalendarFocusRegion.AgendaAdd, FocusedAgendaIndex = null }
                : this;
        }

        if (delta > 0 &&
            (FocusRegion == CalendarFocusRegion.AgendaEmptyState ||
             (FocusRegion == CalendarFocusRegion.AgendaEvents && FocusedAgendaIndex == eventCount - 1)))
        {
            return this with
            {
                FocusRegion = CalendarFocusRegion.AgendaAdd,
                FocusedAgendaIndex = null,
            };
        }

        return MoveAgenda(delta, eventCount);
    }

    public CalendarUiState ReturnToMonth() => this with
    {
        IsAgendaOpen = false,
        FocusRegion = CalendarFocusRegion.MonthGrid,
        FocusedAgendaIndex = null,
        FocusedEventId = null,
    };

    public CalendarUiState FocusPeriodEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("A stable event ID is required.", nameof(eventId));
        }

        return this with
        {
            IsAgendaOpen = false,
            FocusRegion = CalendarFocusRegion.PeriodEvents,
            FocusedAgendaIndex = null,
            FocusedEventId = eventId,
        };
    }

    public CalendarUiState FocusPeriodEmptyState() => this with
    {
        IsAgendaOpen = false,
        FocusRegion = CalendarFocusRegion.PeriodEmptyState,
        FocusedAgendaIndex = null,
        FocusedEventId = null,
    };

    public CalendarUiState FocusTodayControl() => this with
    {
        IsAgendaOpen = false,
        FocusRegion = CalendarFocusRegion.Today,
        FocusedAgendaIndex = null,
        FocusedEventId = null,
    };

    public CalendarUiState ActivateToday(DateOnly today) => this with
    {
        VisibleMonth = new DateOnly(today.Year, today.Month, 1),
        SelectedDate = today,
        IsAgendaOpen = false,
        FocusRegion = CalendarFocusRegion.MonthGrid,
        FocusedAgendaIndex = null,
        FocusedEventId = null,
    };

    public CalendarUiState OpenAgenda() => EnterAgenda(eventCount: 0);

    public CalendarUiState CloseAgenda() => ReturnToMonth();

    public CalendarBackResult HandleBack() =>
        FocusRegion is CalendarFocusRegion.AgendaEvents or CalendarFocusRegion.AgendaEmptyState or CalendarFocusRegion.AgendaAdd or CalendarFocusRegion.AgendaReminders
            ? CalendarBackResult.CloseAgenda
            : CalendarBackResult.ExitApplication;

    public IReadOnlyList<CalendarMonthCell> BuildMonthCells()
    {
        var gridStart = VisibleMonth.AddDays(-(int)VisibleMonth.DayOfWeek);
        return Enumerable.Range(0, 42)
            .Select(offset =>
            {
                var date = gridStart.AddDays(offset);
                return new CalendarMonthCell(date, date.Month == VisibleMonth.Month && date.Year == VisibleMonth.Year);
            })
            .ToArray();
    }
}
