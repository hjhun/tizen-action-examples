using ActionExamples.Ui;
using Calendar.ActionProvider;
using ActionExamples.ViewAnnotations;
using Calendar.Domain;
using Calendar.Persistence;
using Calendar.ScheduleActionProvider;
using Calendar.UseCases;
using Calendar.ViewActionProvider;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Calendar.App;

internal sealed class CalendarApplication : NUIApplication
{
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private CalendarEventRepository? _repository;
    private CalendarReminderRepository? _reminderRepository;
    private CalendarCommandService? _commands;
    private CalendarInteractionState? _interaction;
    private View? _root;
    private CalendarDisplayMetrics? _display;
    private Tizen.NUI.Timer? _annotationTimer;
    private bool _paused;
    private bool _refreshing;
    private UiChangeDispatcher? _changes;
    private bool _renderPending;
    private View? _activeSurfaceRoot;

    protected override void OnCreate()
    {
        base.OnCreate();
        _annotationTimer = new Tizen.NUI.Timer(50);
        _annotationTimer.Tick += OnAnnotationTimer;
        Window.Default.KeyEvent += OnKeyEvent;
        Window.Default.Resized += OnWindowResized;
        Window.Default.Moved += OnWindowMoved;
        Window.Default.InsetsChanged += OnWindowResized;
        FocusManager.Instance.FocusChanged += OnFocusChanged;

        _repository = new CalendarEventRepository([]);
        _reminderRepository = new CalendarReminderRepository([]);
        var dataPath = Tizen.Applications.Application.Current.DirectoryInfo.Data;
        var persistence = new CalendarJsonPersistenceAdapter(
            new CalendarJsonStore(System.IO.Path.Combine(dataPath, "calendar-data.json")));
        _commands = new CalendarCommandService(
            _repository,
            _reminderRepository,
            persistence,
            new TizenReminderAlarmScheduler());
        _commands.Restore();
        _changes = new UiChangeDispatcher(
            SynchronizationContext.Current ?? new Tizen.Applications.TizenSynchronizationContext(), RefreshFromCommands);
        _commands.Changed += _changes.Request;
        CalendarActionProviderHost.Start(_repository, _commands);
        ScheduleReminderActionProviderHost.Start(_reminderRepository, _commands);
        CalendarViewActionProviderHost.Start();

        _interaction = CalendarInteractionState.Create(CalendarUiState.Create(_today));
        Render();
    }

    protected override void OnPause()
    {
        _paused = true;
        _annotationTimer?.Stop();
        CalendarViewActionProviderHost.ClearPublishedViews();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _paused = false;
        RefreshFromCommands();
    }

    protected override void OnTerminate()
    {
        _paused = true;
        if (_commands is not null && _changes is not null) _commands.Changed -= _changes.Request;
        _changes?.Dispose();
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.Resized -= OnWindowResized;
        Window.Default.Moved -= OnWindowMoved;
        Window.Default.KeyEvent -= OnKeyEvent;
        _activeSurfaceRoot = null;
        CalendarViewActionProviderHost.ClearPublishedViews();
        _annotationTimer?.Stop();
        _annotationTimer?.Dispose();
        _annotationTimer = null;
        base.OnTerminate();
    }

    private void OnWindowMoved(object? sender, EventArgs args) => QueueAnnotationRefresh();

    private void OnWindowResized(object? sender, EventArgs eventArgs)
    {
        if (!TryReadDisplay(out var display)) return;
        if (_root is null || _renderPending)
        {
            Render();
            return;
        }

        // Keep editor contents, caret and actual focus alive across resize/insets.
        foreach (var name in new[] { "CalendarDesignCanvas", "CalendarOverlayDesignCanvas" })
        {
            var canvas = _root.FindChildByName(name);
            if (canvas is null) continue;
            canvas.Position = new Position(display.Viewport.OffsetX, display.Viewport.OffsetY);
            canvas.Scale = new Vector3(display.Viewport.Scale, display.Viewport.Scale, 1);
        }
        var overlay = _root.FindChildByName("CalendarOverlay");
        if (overlay is not null) overlay.Size = new Size(display.WindowWidth, display.WindowHeight);
        QueueAnnotationRefresh();
    }

    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs eventArgs) => QueueAnnotationRefresh();

    private void OnKeyEvent(object? sender, Window.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key.State != Key.StateType.Down || _interaction is null)
        {
            return;
        }

        var keyName = eventArgs.Key.KeyPressedName;
        if (_interaction.Surface != CalendarSurface.Calendar)
        {
            if (keyName is "XF86Back" or "Escape")
            {
                _interaction = _interaction.Back();
                Render();
            }

            return;
        }

        var state = _interaction.Calendar;
        switch (keyName)
        {
            case "Left":
                state = state.IsHeaderFocused
                    ? state.MoveHeaderFocus(-1)
                    : state.FocusRegion == CalendarFocusRegion.PeriodEvents
                        ? MovePeriodEventFocus(state, -1)
                        : IsAgendaFocused(state)
                            ? state.ReturnToMonth()
                            : state.MoveDays(-1);
                break;
            case "Right":
                if (state.IsHeaderFocused)
                {
                    state = state.MoveHeaderFocus(1);
                    break;
                }

                if (state.FocusRegion != CalendarFocusRegion.MonthGrid)
                {
                    if (state.FocusRegion == CalendarFocusRegion.PeriodEvents)
                    {
                        state = MovePeriodEventFocus(state, 1);
                        break;
                    }

                    return;
                }

                state = state.MoveDays(1);
                break;
            case "Up":
                state = HandleUp(state);
                break;
            case "Down":
                state = HandleDown(state);
                break;
            case "Return":
            case "Enter":
            case "XF86Select":
                HandleEnter(state);
                Render();
                return;
            case "XF86Back":
            case "Escape":
                if (state.HandleBack() == CalendarBackResult.ExitApplication)
                {
                    Exit();
                    return;
                }

                state = state.ReturnToMonth();
                break;
            default:
                return;
        }

        UpdateCalendar(state);
        Render();
    }

    private CalendarUiState HandleUp(CalendarUiState state)
    {
        if (state.IsHeaderFocused)
        {
            return state;
        }

        if (state.FocusRegion is CalendarFocusRegion.AgendaEvents or CalendarFocusRegion.AgendaEmptyState or CalendarFocusRegion.AgendaAdd or CalendarFocusRegion.AgendaReminders)
        {
            return state.MoveAgendaFocus(-1, GetSelectedEvents(state).Count);
        }

        if (state.FocusRegion == CalendarFocusRegion.PeriodEvents)
        {
            var events = GetPeriodEvents(state);
            var index = events.ToList().FindIndex(calendarEvent => calendarEvent.Id == state.FocusedEventId);
            return index <= 0
                ? state.FocusHeader(GetModeFocusRegion(state.ViewMode))
                : state.FocusPeriodEvent(events[index - 1].Id);
        }

        if (state.FocusRegion == CalendarFocusRegion.PeriodEmptyState || state.ViewMode != CalendarViewMode.Month)
        {
            return state.FocusHeader(GetModeFocusRegion(state.ViewMode));
        }

        return state.BuildMonthCells().Take(7).Any(cell => cell.Date == state.SelectedDate)
            ? state.FocusHeader(GetModeFocusRegion(state.ViewMode))
            : state.MoveDays(-7);
    }

    private CalendarUiState HandleDown(CalendarUiState state)
    {
        if (state.FocusRegion is CalendarFocusRegion.AgendaEvents or CalendarFocusRegion.AgendaEmptyState or CalendarFocusRegion.AgendaAdd or CalendarFocusRegion.AgendaReminders)
        {
            return state.MoveAgendaFocus(1, GetSelectedEvents(state).Count);
        }

        if (state.IsHeaderFocused && state.ViewMode != CalendarViewMode.Month)
        {
            var events = GetPeriodEvents(state);
            return events.Count == 0
                ? state.FocusPeriodEmptyState()
                : state.FocusPeriodEvent(events[0].Id);
        }

        if (state.FocusRegion == CalendarFocusRegion.PeriodEvents)
        {
            return MovePeriodEventFocus(state, 1);
        }

        return state.IsHeaderFocused
            ? state.ReturnToMonth()
            : state.ViewMode == CalendarViewMode.Month
                ? state.MoveDays(7)
                : state;
    }

    private static CalendarFocusRegion GetModeFocusRegion(CalendarViewMode viewMode) => viewMode switch
    {
        CalendarViewMode.Month => CalendarFocusRegion.MonthMode,
        CalendarViewMode.Week => CalendarFocusRegion.WeekMode,
        CalendarViewMode.Day => CalendarFocusRegion.DayMode,
        CalendarViewMode.Agenda => CalendarFocusRegion.AgendaMode,
        _ => CalendarFocusRegion.MonthMode,
    };

    private void HandleEnter(CalendarUiState state)
    {
        if (_interaction is null)
        {
            return;
        }

        if (state.IsHeaderFocused)
        {
            switch (state.FocusRegion)
            {
                case CalendarFocusRegion.PreviousPeriod:
                    UpdateCalendar(state.MovePeriod(-1));
                    break;
                case CalendarFocusRegion.Today:
                    UpdateCalendar(state.ActivateToday(_today).FocusHeader(CalendarFocusRegion.Today));
                    break;
                case CalendarFocusRegion.NextPeriod:
                    UpdateCalendar(state.MovePeriod(1));
                    break;
                case CalendarFocusRegion.Search:
                    OpenSearch();
                    break;
                case CalendarFocusRegion.MonthMode:
                    UpdateCalendar(state.ChangeViewMode(CalendarViewMode.Month));
                    break;
                case CalendarFocusRegion.WeekMode:
                    UpdateCalendar(state.ChangeViewMode(CalendarViewMode.Week));
                    break;
                case CalendarFocusRegion.DayMode:
                    UpdateCalendar(state.ChangeViewMode(CalendarViewMode.Day));
                    break;
                case CalendarFocusRegion.AgendaMode:
                    UpdateCalendar(state.ChangeViewMode(CalendarViewMode.Agenda));
                    break;
            }

            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.MonthGrid)
        {
            var events = GetSelectedEvents(state);
            if (state.ViewMode == CalendarViewMode.Month)
            {
                UpdateCalendar(state.EnterAgenda(events.Count));
            }
            else if (events.FirstOrDefault() is { } firstEvent)
            {
                OpenEventDetail(firstEvent.Id);
            }

            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.PeriodEvents && state.FocusedEventId is { } focusedEventId)
        {
            OpenEventDetail(focusedEventId);
            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.PeriodEmptyState)
        {
            OpenNewEvent();
            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.AgendaAdd)
        {
            OpenNewEvent();
            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.AgendaReminders)
        {
            OpenReminderList();
            return;
        }

        if (state.FocusRegion == CalendarFocusRegion.AgendaEvents && state.FocusedAgendaIndex is int index)
        {
            OpenEventDetail(index);
        }
    }

    private IReadOnlyList<CalendarEvent> GetSelectedEvents(CalendarUiState? state = null)
    {
        if (_repository is null || (state ?? _interaction?.Calendar) is not { } calendarState)
        {
            return Array.Empty<CalendarEvent>();
        }

        var date = calendarState.SelectedDate;
        var start = CalendarDateBoundary.AtStartOfDay(date);
        var end = CalendarDateBoundary.AtStartOfDay(date.AddDays(1));
        return _repository.GetEventsOverlapping(start, end);
    }

    private IReadOnlyList<CalendarEvent> GetPeriodEvents(CalendarUiState state)
    {
        if (_repository is null)
        {
            return Array.Empty<CalendarEvent>();
        }

        if (state.ViewMode == CalendarViewMode.Month)
        {
            return GetSelectedEvents(state);
        }

        var presentation = CalendarPeriodPresentation.Create(state, _repository, _today);
        var agendaHeight = CalendarLayoutMetrics.CalculateMainHeight(CalendarTheme.Light);
        var renderedIds = CalendarPeriodRenderPolicy.GetRenderedEventIds(presentation, agendaHeight);
        return _repository.ResolveByIds(renderedIds).Events;
    }

    private CalendarUiState MovePeriodEventFocus(CalendarUiState state, int delta)
    {
        var events = GetPeriodEvents(state);
        if (events.Count == 0)
        {
            return state.FocusPeriodEmptyState();
        }

        var current = events.ToList().FindIndex(calendarEvent => calendarEvent.Id == state.FocusedEventId);
        var target = Math.Clamp((current < 0 ? 0 : current) + delta, 0, events.Count - 1);
        return state.FocusPeriodEvent(events[target].Id);
    }

    private static bool IsAgendaFocused(CalendarUiState state) =>
        state.FocusRegion is CalendarFocusRegion.AgendaEvents or CalendarFocusRegion.AgendaEmptyState or CalendarFocusRegion.AgendaAdd or CalendarFocusRegion.AgendaReminders;

    private void Dispatch(CalendarUiCommand command)
    {
        if (_interaction is null)
        {
            return;
        }

        var state = CalendarUiReducer.Reduce(
            _interaction.Calendar,
            command,
            _today,
            GetSelectedEvents().Count);
        UpdateCalendar(state);

        if (command is CalendarUiCommand.SelectAgendaEvent selected)
        {
            OpenEventDetail(selected.Index);
        }
        else if (command is CalendarUiCommand.OpenEvent openEvent)
        {
            OpenEventDetail(openEvent.EventId);
        }

        Render();
    }

    private void OpenNewEvent()
    {
        if (_interaction is null)
        {
            return;
        }

        _interaction = _interaction.OpenNewEvent();
    }

    private void OpenEventDetail(int index)
    {
        if (_interaction is null)
        {
            return;
        }

        var events = GetSelectedEvents();
        if (index < 0 || index >= events.Count)
        {
            return;
        }

        _interaction = _interaction.OpenEventDetail(events[index].Id);
    }

    private void OpenEventDetail(string eventId)
    {
        if (_interaction is null || _repository?.ResolveByIds([eventId]).Events.Count != 1)
        {
            return;
        }

        _interaction = _interaction.OpenEventDetail(eventId);
    }

    private void OpenReminderList()
    {
        if (_interaction?.Surface != CalendarSurface.Calendar)
        {
            return;
        }

        _interaction = _interaction.OpenReminderList();
    }

    private void SaveEvent(CalendarEditorState editor)
    {
        if (_commands is null || _interaction is null || !editor.CanSave)
        {
            return;
        }

        var eventId = editor.EventId ?? $"event-{Guid.NewGuid():N}";
        var calendarEvent = CalendarEvent.Create(
            eventId,
            editor.Title,
            editor.Start,
            editor.End,
            editor.Note,
            editor.Location);
        var result = editor.IsEditing
            ? _commands.UpdateEvent(calendarEvent, editor.ReminderOffsets)
            : _commands.CreateEvent(calendarEvent, editor.ReminderOffsets);
        if (!result.Success)
        {
            return;
        }

        var selectedDate = DateOnly.FromDateTime(calendarEvent.Start.Date);
        var calendar = CalendarUiReducer.Reduce(
            _interaction.Calendar,
            new CalendarUiCommand.SelectDate(selectedDate),
            _today,
            GetSelectedEvents().Count);
        _interaction = _interaction with
        {
            Calendar = calendar,
            Surface = CalendarSurface.EventDetail,
            SelectedEventId = calendarEvent.Id,
            EventEditor = null,
        };
        Render();
    }

    private void EditSelectedEvent()
    {
        if (_repository is null || _reminderRepository is null || _interaction?.SelectedEventId is not { } eventId)
        {
            return;
        }

        var calendarEvent = _repository.ResolveByIds([eventId]).Events.SingleOrDefault();
        if (calendarEvent is null)
        {
            return;
        }

        var offsets = _reminderRepository.FindByCalendarEventId(eventId)
            .Where(reminder => reminder.OffsetMinutes is not null)
            .Select(reminder => reminder.OffsetMinutes!.Value);
        _interaction = _interaction.OpenEventEditor(calendarEvent, offsets);
        Render();
    }

    private void RequestSelectedEventDelete()
    {
        if (_interaction?.Surface != CalendarSurface.EventDetail)
        {
            return;
        }

        _interaction = _interaction.RequestEventDelete();
        Render();
    }

    private void ConfirmSelectedEventDelete()
    {
        if (_commands is null || _interaction?.SelectedEventId is not { } eventId)
        {
            return;
        }

        var result = _commands.DeleteEvent(eventId);
        if (!result.Success)
        {
            return;
        }

        _interaction = _interaction with
        {
            Surface = CalendarSurface.Calendar,
            SelectedEventId = null,
            EventEditor = null,
        };
        Render();
    }

    private void CancelSelectedEventDelete()
    {
        if (_interaction?.Surface != CalendarSurface.DeleteEventConfirmation)
        {
            return;
        }

        _interaction = _interaction.CancelEventDelete();
        Render();
    }

    private void OpenNewReminder()
    {
        if (_interaction?.Surface != CalendarSurface.ReminderList)
        {
            return;
        }

        var now = DateTimeOffset.Now.AddHours(1);
        var suggestedDue = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset);
        _interaction = _interaction.OpenNewReminder(suggestedDue);
        Render();
    }

    private void EditReminder(string reminderId)
    {
        if (_interaction?.Surface != CalendarSurface.ReminderList || _reminderRepository?.Find(reminderId) is not { } reminder)
        {
            return;
        }

        _interaction = _interaction.OpenReminderEditor(reminder);
        Render();
    }

    private void SaveReminder(CalendarReminderEditorState editor)
    {
        if (_commands is null || _interaction is null || !editor.CanSave)
        {
            return;
        }

        var reminder = editor.ToDomain($"reminder-{Guid.NewGuid():N}");
        var result = editor.IsEditing
            ? _commands.UpdateReminder(reminder)
            : _commands.CreateReminder(reminder);
        if (!result.Success)
        {
            return;
        }

        _interaction = _interaction with
        {
            Surface = CalendarSurface.ReminderList,
            SelectedReminderId = null,
            ReminderEditor = null,
        };
        Render();
    }

    private void RequestReminderDelete()
    {
        if (_interaction?.Surface != CalendarSurface.ReminderEditor || _interaction.SelectedReminderId is null)
        {
            return;
        }

        _interaction = _interaction.RequestReminderDelete();
        Render();
    }

    private void ConfirmReminderDelete()
    {
        if (_commands is null || _interaction?.SelectedReminderId is not { } reminderId)
        {
            return;
        }

        if (!_commands.DeleteReminder(reminderId).Success)
        {
            return;
        }

        _interaction = _interaction with
        {
            Surface = CalendarSurface.ReminderList,
            SelectedReminderId = null,
            ReminderEditor = null,
        };
        Render();
    }

    private void ToggleReminderCompletion(string reminderId)
    {
        if (_commands is null || _reminderRepository?.Find(reminderId) is not { } reminder)
        {
            return;
        }

        _commands.SetReminderCompleted(reminderId, !reminder.IsCompleted);
        Render();
    }

    private void OpenSearch()
    {
        if (_interaction?.Surface != CalendarSurface.Calendar)
        {
            return;
        }

        _interaction = _interaction.OpenSearch();
        Render();
    }

    private void ApplySearch(CalendarSearchState search)
    {
        if (_interaction?.Surface != CalendarSurface.Search || _repository is null)
        {
            return;
        }

        _interaction = _interaction with
        {
            Search = search.Apply(_repository),
            SearchReturnEventId = null,
        };
        Render();
    }

    private void OpenSearchResult(string eventId)
    {
        if (_interaction?.Surface != CalendarSurface.Search || _repository?.ResolveByIds([eventId]).Events.Count != 1)
        {
            return;
        }

        _interaction = _interaction.OpenSearchResult(eventId);
        Render();
    }

    private void UpdateCalendar(CalendarUiState state)
    {
        if (_interaction is not null)
        {
            _interaction = _interaction with { Calendar = state };
        }
    }

    private bool TryReadDisplay(out CalendarDisplayMetrics display)
    {
        display = default;
        int screenWidth = 0, screenHeight = 0;
        try
        {
            var hasWidth = Tizen.System.Information.TryGetValue("http://tizen.org/feature/screen.width", out screenWidth);
            var hasHeight = Tizen.System.Information.TryGetValue("http://tizen.org/feature/screen.height", out screenHeight);
            if (!hasWidth || !hasHeight) screenWidth = screenHeight = 0;
        }
        catch (Exception exception)
        {
            Tizen.Log.Warn("Calendar", $"Screen size unavailable: {exception.Message}");
        }

        try
        {
            var size = Window.Default.WindowSize;
            var insets = Window.Default.GetInsets();
            if (!CalendarDisplayMetrics.TryCreate(size.Width, size.Height, screenWidth, screenHeight,
                    insets.Start, insets.Top, insets.End, insets.Bottom, out display)) return false;
            if (_display != display)
            {
                Tizen.Log.Info("Calendar", $"Screen={display.ScreenWidth}x{display.ScreenHeight}, " +
                    $"Window={display.WindowWidth}x{display.WindowHeight}, Insets={insets.Start}/{insets.Top}/{insets.End}/{insets.Bottom}, " +
                    $"CanvasScale={display.Viewport.Scale}");
                _display = display;
            }
            return true;
        }
        catch (Exception exception)
        {
            Tizen.Log.Warn("Calendar", $"Window geometry unavailable: {exception.Message}");
            return false;
        }
    }

    private void RefreshAppliedSearch()
    {
        if (_interaction is null || _repository is null) return;
        if (_interaction.Surface == CalendarSurface.Search &&
            _interaction.Search is { HasApplied: true } appliedSearch &&
            appliedSearch.AppliedRepositoryVersion != _repository.Version)
        {
            var refreshed = appliedSearch.Apply(_repository);
            _interaction = _interaction with
            {
                Search = refreshed,
                SearchReturnEventId = _interaction.SearchReturnEventId is { } returnId &&
                    refreshed.ResultEventIds.Contains(returnId, StringComparer.Ordinal)
                        ? returnId
                        : null,
            };
        }
    }

    private void RefreshFromCommands()
    {
        if (_paused || _interaction is null || _repository is null) return;
        if (_root is null || _activeSurfaceRoot is null) { Render(); return; }
        var focused = NuiViewAnnotations.Focused(_activeSurfaceRoot);
        var focusKey = NuiViewAnnotations.Descendants(_activeSurfaceRoot).FirstOrDefault(x => x.View == focused).Key;
        _refreshing = true;
        try
        {
            if (_interaction.Surface is CalendarSurface.EventEditor or CalendarSurface.ReminderEditor or CalendarSurface.Search)
            {
                if (!TryReadDisplay(out var display)) { _renderPending = true; return; }
                RefreshAppliedSearch();
                // Keep editor/search actors attached: their closures, invalid text,
                // cursor, selection and native IME focus are the live user draft.
                using var replacement = CalendarMonthView.Create(display, _interaction.Calendar, _repository, _today,
                    Dispatch, () => { OpenNewEvent(); Render(); }, () => { OpenReminderList(); Render(); }, OpenSearch, out _);
                var canvas = replacement.FindChildByName("CalendarDesignCanvas");
                replacement.Remove(canvas);
                var previous = _root.FindChildByName("CalendarDesignCanvas");
                _root.Remove(previous); previous.Dispose();
                canvas.Sensitive = false; canvas.FocusableChildren = false;
                _root.Add(canvas);
                _activeSurfaceRoot.RaiseToTop();
                if (_interaction.Surface == CalendarSurface.Search)
                    CalendarOverlayView.RefreshSearchResults(_activeSurfaceRoot, _interaction.Search!, _repository, OpenSearchResult);
            }
            else Render();

            // Resolve a stable actor path, not its previous list index. Retained
            // input actors need no focus call (which would disturb their IME).
            var target = NuiViewAnnotations.Descendants(_activeSurfaceRoot!).FirstOrDefault(x => x.Key == focusKey).View;
            if (target is not null && target != focused && target.Focusable && target.IsEnabled)
                FocusManager.Instance.SetCurrentFocusView(target);
            else if (target is null && _interaction.Surface == CalendarSurface.Search)
                FocusManager.Instance.SetCurrentFocusView(_activeSurfaceRoot!.FindChildByName("CalendarSearchKeyword"));
        }
        finally { _refreshing = false; QueueAnnotationRefresh(); }
    }

    private void Render()
    {
        if (_paused) return;
        if (_interaction is null || _repository is null)
        {
            return;
        }
        if (!TryReadDisplay(out var display))
        {
            _renderPending = true;
            return;
        }
        _renderPending = false;

        RefreshAppliedSearch();

        _activeSurfaceRoot = null;
        CalendarViewActionProviderHost.ClearPublishedViews();
        if (_root is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_root);
            _root.Dispose();
        }

        _root = CalendarMonthView.Create(
            display,
            _interaction.Calendar,
            _repository,
            _today,
            Dispatch,
            () =>
            {
                OpenNewEvent();
                Render();
            },
            () =>
            {
                OpenReminderList();
                Render();
            },
            OpenSearch,
            out var preferredFocus);

        View activeSurfaceRoot = _root;
        if (_interaction.Surface != CalendarSurface.Calendar)
        {
            var backgroundCanvas = _root.FindChildByName("CalendarDesignCanvas");
            if (backgroundCanvas is not null)
            {
                backgroundCanvas.Sensitive = false;
                backgroundCanvas.FocusableChildren = false;
            }
            var overlay = CalendarOverlayView.Create(
                display,
                _interaction,
                _repository,
                _reminderRepository!,
                close: () =>
                {
                    _interaction = _interaction?.Back();
                    Render();
                },
                saveEvent: SaveEvent,
                editEvent: EditSelectedEvent,
                requestDelete: RequestSelectedEventDelete,
                confirmDelete: ConfirmSelectedEventDelete,
                cancelDelete: CancelSelectedEventDelete,
                openNewReminder: OpenNewReminder,
                editReminder: EditReminder,
                saveReminder: SaveReminder,
                requestReminderDelete: RequestReminderDelete,
                confirmReminderDelete: ConfirmReminderDelete,
                toggleReminderCompletion: ToggleReminderCompletion,
                applySearch: ApplySearch,
                openSearchResult: OpenSearchResult);
            _root.Add(overlay);
            activeSurfaceRoot = overlay;
        }

        Window.Default.GetDefaultLayer().Add(_root);
        _activeSurfaceRoot = activeSurfaceRoot;
        NuiViewAnnotations.Observe(activeSurfaceRoot, QueueAnnotationRefresh);
        if (_interaction.Surface == CalendarSurface.Calendar && _interaction.Calendar.FocusedEventId is { } focusedEventId)
        {
            preferredFocus = _root.FindChildByName($"CalendarEvent-{focusedEventId}") ?? preferredFocus;
        }

        if (_interaction.Surface == CalendarSurface.Calendar && preferredFocus is not null)
        {
            FocusManager.Instance.SetCurrentFocusView(preferredFocus);
        }
        else if (_interaction.Surface != CalendarSurface.Calendar)
        {
            var overlayFocus = _interaction.Surface == CalendarSurface.Search
                ? (_interaction.SearchReturnEventId is { } returnEventId
                    ? activeSurfaceRoot.FindChildByName($"CalendarEvent-{returnEventId}")
                    : null) ?? activeSurfaceRoot.FindChildByName("CalendarSearchKeyword")
                : activeSurfaceRoot.FindChildByName("CalendarOverlayClose") ?? activeSurfaceRoot;
            FocusManager.Instance.SetCurrentFocusView(overlayFocus);
        }

        QueueAnnotationRefresh();
    }

    private void QueueAnnotationRefresh()
    {
        _annotationTimer?.Stop();
        if (_paused || _refreshing || _activeSurfaceRoot is not { } surface) return;
        // Focus and text changes already describe a live tree. Replace its snapshot
        // atomically now, then measure again after DALi commits the next layout.
        // Only Render/OnPause invalidate a removed or hidden tree.
        PublishMeasuredViews(surface);
        _annotationTimer?.Start();
    }

    private bool OnAnnotationTimer(object? sender, Tizen.NUI.Timer.TickEventArgs args)
    {
        if (!_paused && !_refreshing && _activeSurfaceRoot is { } surface) PublishMeasuredViews(surface);
        return false;
    }

    private void PublishMeasuredViews(View activeSurfaceRoot)
    {
        if (_interaction is null) return;
        var state = _interaction;
        var context = CalendarAnnotationPage.Create(state);
        var surface = context.Surface;
        var pageId = context.Id;
        var page = context.State;
        var snapshots = new List<CurrentViewSnapshot>();
        var annotated = new HashSet<View>();
        var focused = NuiViewAnnotations.Focused(activeSurfaceRoot);
        void Capture(View? view, CurrentViewSnapshot snapshot)
        {
            if (view is null) return;
            var measured = NuiViewAnnotations.Measure(view, activeSurfaceRoot, focused, snapshot);
            if (measured is null) return;
            snapshots.Add(measured);
            annotated.Add(view);
        }
        var pane = activeSurfaceRoot.FindChildByName("CalendarOverlayPane") ?? activeSurfaceRoot.FindChildByName("CalendarDesignCanvas") ?? activeSurfaceRoot;
        Capture(pane, CalendarViewSnapshots.Context(pageId, "Calendar.Page", surface, new { schemaVersion = 1, page }));
        var events = GetVisibleEvents().ToDictionary(x => x.Id, StringComparer.Ordinal);
        var reminders = state.Surface == CalendarSurface.ReminderList
            ? _reminderRepository!.Snapshot().Where(x => x.CalendarEventId is null).ToDictionary(x => x.Id, StringComparer.Ordinal)
            : new Dictionary<string, CalendarReminder>();
        foreach (var (view, key) in NuiViewAnnotations.Descendants(activeSurfaceRoot))
        {
            var name = view.Name ?? string.Empty;
            const string eventPrefix = "CalendarEvent-";
            const string reminderPrefix = "CalendarReminder-";
            if (name.StartsWith(eventPrefix, StringComparison.Ordinal) && events.TryGetValue(name[eventPrefix.Length..], out var item))
                Capture(view, CalendarViewSnapshots.Event($"calendar:{surface}:event:{item.Id}:{key}", item, state.Surface == CalendarSurface.EventDetail));
            else if (name.StartsWith(reminderPrefix, StringComparison.Ordinal) && reminders.TryGetValue(name[reminderPrefix.Length..], out var reminderItem))
                Capture(view, CalendarViewSnapshots.Reminder($"calendar:{surface}:reminder:{reminderItem.Id}", reminderItem, includeNote: false));
        }
        if (state.Surface == CalendarSurface.DeleteReminderConfirmation && state.SelectedReminderId is { } reminderId && _reminderRepository!.Find(reminderId) is { } reminder)
            Capture(activeSurfaceRoot.FindChildByName("CalendarReminderConfirmation"),
                CalendarViewSnapshots.Reminder($"calendar:{surface}:reminder:{reminder.Id}", reminder, includeNote: false));
        // Controls describe the live draft, never a saved entity pretending to be unsaved input.
        foreach (var (view, key) in NuiViewAnnotations.Controls(activeSurfaceRoot))
        {
            if (annotated.Contains(view)) continue;
            Capture(view, CalendarViewSnapshots.Context($"{pageId}:control:{key}", "Calendar.Control",
                NuiViewAnnotations.Description(view, key), new { schemaVersion = 1, page, draft = NuiViewAnnotations.ControlState(view, key) }));
        }
        CalendarViewActionProviderHost.Publish(snapshots);
    }

    private IReadOnlyList<CalendarEvent> GetVisibleEvents()
    {
        if (_repository is null || _interaction is null)
        {
            return Array.Empty<CalendarEvent>();
        }

        if (_interaction.Surface == CalendarSurface.Search && _interaction.Search is { HasApplied: true } search)
        {
            return _repository.ResolveByIds(search.ResultEventIds).Events;
        }

        if (_interaction.Surface != CalendarSurface.Calendar && _interaction.SelectedEventId is { } selectedEventId)
        {
            return _repository.ResolveByIds([selectedEventId]).Events;
        }

        var state = _interaction.Calendar;
        DateOnly rangeStart;
        DateOnly rangeEndExclusive;
        switch (state.ViewMode)
        {
            case CalendarViewMode.Week:
                rangeStart = state.SelectedDate.AddDays(-(int)state.SelectedDate.DayOfWeek);
                rangeEndExclusive = rangeStart.AddDays(7);
                break;
            case CalendarViewMode.Day:
                rangeStart = state.SelectedDate;
                rangeEndExclusive = rangeStart.AddDays(1);
                break;
            case CalendarViewMode.Agenda:
                rangeStart = state.VisibleMonth;
                rangeEndExclusive = rangeStart.AddMonths(1);
                break;
            default:
                var cells = state.BuildMonthCells();
                rangeStart = cells[0].Date;
                rangeEndExclusive = cells[^1].Date.AddDays(1);
                break;
        }

        var start = CalendarDateBoundary.AtStartOfDay(rangeStart);
        var end = CalendarDateBoundary.AtStartOfDay(rangeEndExclusive);
        return _repository.GetEventsOverlapping(start, end);
    }

    private static void Main(string[] args)
    {
        var app = new CalendarApplication();
        app.Run(args);
    }
}
