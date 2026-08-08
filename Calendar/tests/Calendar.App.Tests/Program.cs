using Calendar.App;
using Calendar.Domain;
using System.Text.Json;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var state = CalendarUiState.Create(new DateOnly(2026, 8, 10));
var grid = state.BuildMonthCells();
Assert(
    grid.Count == 42 && grid[0].Date == new DateOnly(2026, 7, 26) && grid[^1].Date == new DateOnly(2026, 9, 5),
    "Calendar month grid must always render six Sunday-first weeks.");
Assert(state.FocusRegion == CalendarFocusRegion.MonthGrid, "Initial focus must be the month grid.");
Assert(state.ViewMode == CalendarViewMode.Month, "Initial calendar view must be Month.");

var periodState = CalendarUiState.Create(new DateOnly(2024, 1, 31))
    .FocusHeader(CalendarFocusRegion.NextPeriod)
    .MovePeriod(1);
Assert(
    periodState.SelectedDate == new DateOnly(2024, 2, 29) &&
    periodState.VisibleMonth == new DateOnly(2024, 2, 1) &&
    periodState.FocusRegion == CalendarFocusRegion.NextPeriod,
    "Next-month navigation must clamp the selected day and preserve repeatable header focus.");
periodState = periodState.MovePeriod(-1);
Assert(
    periodState.SelectedDate == new DateOnly(2024, 1, 29) && periodState.VisibleMonth == new DateOnly(2024, 1, 1),
    "Returning from a clamped month must preserve the currently selected day.");
periodState = CalendarUiState.Create(new DateOnly(2026, 1, 15)).MovePeriod(-1);
Assert(
    periodState.SelectedDate == new DateOnly(2025, 12, 15) && periodState.VisibleMonth == new DateOnly(2025, 12, 1),
    "Previous-month navigation must cross the year boundary.");

periodState = CalendarUiState.Create(new DateOnly(2026, 8, 10)).ChangeViewMode(CalendarViewMode.Week);
Assert(
    periodState.ViewMode == CalendarViewMode.Week && periodState.SelectedDate == new DateOnly(2026, 8, 10),
    "Changing view mode must preserve the selected date anchor.");
periodState = periodState.MovePeriod(1);
Assert(periodState.SelectedDate == new DateOnly(2026, 8, 17), "Week next must move the anchor by seven days.");
periodState = periodState.ChangeViewMode(CalendarViewMode.Day).MovePeriod(-1);
Assert(periodState.SelectedDate == new DateOnly(2026, 8, 16), "Day previous must move the anchor by one day.");
periodState = periodState.ChangeViewMode(CalendarViewMode.Agenda).MovePeriod(1);
Assert(
    periodState.SelectedDate == new DateOnly(2026, 9, 16) && periodState.VisibleMonth == new DateOnly(2026, 9, 1),
    "Agenda next must move its range by one month.");

var reducerPeriodState = CalendarUiReducer.Reduce(
    CalendarUiState.Create(new DateOnly(2026, 8, 31)).FocusHeader(CalendarFocusRegion.NextPeriod),
    new CalendarUiCommand.ShowNextPeriod(),
    today: new DateOnly(2026, 8, 8),
    selectedDateEventCount: 0);
Assert(
    reducerPeriodState.SelectedDate == new DateOnly(2026, 9, 30) && reducerPeriodState.FocusRegion == CalendarFocusRegion.NextPeriod,
    "Pointer and remote next-period activation must share the reducer transition.");
reducerPeriodState = CalendarUiReducer.Reduce(
    reducerPeriodState,
    new CalendarUiCommand.ChangeViewMode(CalendarViewMode.Day),
    today: new DateOnly(2026, 8, 8),
    selectedDateEventCount: 0);
Assert(reducerPeriodState.ViewMode == CalendarViewMode.Day, "View mode tabs must dispatch an explicit reducer command.");

var headerState = CalendarUiState.Create(new DateOnly(2026, 8, 10))
    .FocusHeader(CalendarFocusRegion.PreviousPeriod)
    .MoveHeaderFocus(1)
    .MoveHeaderFocus(1)
    .MoveHeaderFocus(1);
Assert(headerState.FocusRegion == CalendarFocusRegion.MonthMode, "Header Right must follow Previous, Today, Next, Month order.");
headerState = headerState.MoveHeaderFocus(1).MoveHeaderFocus(1).MoveHeaderFocus(1).MoveHeaderFocus(1);
Assert(headerState.FocusRegion == CalendarFocusRegion.Search, "Header navigation must reach every view mode before Search.");
Assert(headerState.MoveHeaderFocus(1).FocusRegion == CalendarFocusRegion.Search, "Header focus must remain bounded at its final control.");

state = state.MoveDays(1);
Assert(
    state.SelectedDate == new DateOnly(2026, 8, 11) && state.VisibleMonth == new DateOnly(2026, 8, 1),
    "Right navigation must move to the adjacent date without changing the visible month.");

state = CalendarUiState.Create(new DateOnly(2026, 8, 31)).MoveDays(1);
Assert(
    state.SelectedDate == new DateOnly(2026, 9, 1) && state.VisibleMonth == new DateOnly(2026, 9, 1),
    "Date navigation across a month boundary must follow the selected date.");

state = CalendarUiState.Create(new DateOnly(2026, 8, 10)).EnterAgenda(eventCount: 2);
Assert(
    state.IsAgendaOpen && state.FocusRegion == CalendarFocusRegion.AgendaEvents && state.FocusedAgendaIndex == 0,
    "Enter on an event-bearing date must focus the first agenda event.");
Assert(state.HandleBack() == CalendarBackResult.CloseAgenda, "Back from agenda must close it before root exit.");

state = state.MoveAgenda(1, eventCount: 2);
Assert(state.FocusedAgendaIndex == 1, "Agenda Down must focus the next event.");
state = state.MoveAgenda(1, eventCount: 2);
Assert(state.FocusedAgendaIndex == 1, "Agenda focus must remain bounded at the final event.");
state = state.MoveAgenda(-3, eventCount: 2);
Assert(state.FocusedAgendaIndex == 0, "Agenda focus must remain bounded at the first event.");

var restoredDate = state.SelectedDate;
state = state.ReturnToMonth();
Assert(
    state.FocusRegion == CalendarFocusRegion.MonthGrid && !state.IsAgendaOpen && state.SelectedDate == restoredDate,
    "Returning from agenda must restore the exact selected date.");
Assert(state.HandleBack() == CalendarBackResult.ExitApplication, "Back from the root grid must request exit.");

state = CalendarUiState.Create(new DateOnly(2026, 8, 18)).EnterAgenda(eventCount: 0);
Assert(
    state.FocusRegion == CalendarFocusRegion.AgendaEmptyState && state.FocusedAgendaIndex is null,
    "Enter on an empty date must focus the No events state.");

state = CalendarUiState.Create(new DateOnly(2026, 8, 2)).FocusTodayControl();
Assert(state.FocusRegion == CalendarFocusRegion.Today, "Up from the top month row must focus Today.");
state = state.ActivateToday(new DateOnly(2026, 9, 7));
Assert(
    state.FocusRegion == CalendarFocusRegion.MonthGrid &&
    state.SelectedDate == new DateOnly(2026, 9, 7) &&
    state.VisibleMonth == new DateOnly(2026, 9, 1),
    "Activating Today must select today, update the visible month, and restore grid focus.");

var day = CalendarDateBoundary.AtStartOfDay(new DateOnly(2026, 8, 10));
var repository = new CalendarEventRepository(
[
    CalendarEvent.Create("all-day", "Company holiday", day, day.AddDays(1), string.Empty, string.Empty),
    CalendarEvent.Create("standup", "Daily stand-up", day.AddHours(9), day.AddHours(10), string.Empty, "Studio"),
    CalendarEvent.Create("lunch", "Lunch", day.AddHours(12), day.AddHours(13), string.Empty, "Cafeteria"),
    CalendarEvent.Create("review", "Design review", day.AddHours(15), day.AddHours(16), string.Empty, "Meeting room"),
]);
state = CalendarUiState.Create(new DateOnly(2026, 8, 10));
var presentation = CalendarMonthPresentation.Create(state, repository, new DateOnly(2026, 8, 10));
Assert(presentation.Cells.Count == 42, "Month presentation must contain exactly 42 independent date cells.");
var selectedCell = presentation.Cells.Single(cell => cell.Date == state.SelectedDate);
Assert(
    selectedCell.IsToday && selectedCell.IsSelected && selectedCell.IsFocused && selectedCell.IsInVisibleMonth,
    "Today, selection, focus, and in-month state must be independent and simultaneously representable.");
Assert(selectedCell.EventChips.Count == 2 && selectedCell.OverflowCount == 2, "Date cells must show two chips and the exact hidden-event count.");
Assert(presentation.Agenda.Events[0].IsAllDay, "All-day events must appear before timed events.");
Assert(
    presentation.Agenda.Events.Select(item => item.EventId).SequenceEqual(["all-day", "standup", "lunch", "review"]),
    "Agenda events must remain in chronological and stable-ID order.");

var emptyPresentation = CalendarMonthPresentation.Create(
    CalendarUiState.Create(new DateOnly(2026, 8, 11)).EnterAgenda(eventCount: 0),
    repository,
    new DateOnly(2026, 8, 10));
Assert(emptyPresentation.Agenda.IsEmpty && emptyPresentation.Agenda.EmptyStateText == "No events", "Empty dates must expose a focusable No events presentation.");

var weekState = CalendarUiState.Create(new DateOnly(2026, 8, 10)).ChangeViewMode(CalendarViewMode.Week);
var weekPresentation = CalendarPeriodPresentation.Create(weekState, repository, new DateOnly(2026, 8, 10));
Assert(
    weekPresentation.RangeStart == new DateOnly(2026, 8, 9) &&
    weekPresentation.RangeEndExclusive == new DateOnly(2026, 8, 16) &&
    weekPresentation.Days.Count == 7,
    "Week presentation must expose a Sunday-first seven-day range.");
Assert(
    weekPresentation.Days.Single(dayPresentation => dayPresentation.Date == new DateOnly(2026, 8, 10)).Events.Count == 4,
    "Week presentation must group every overlapping event under its day.");

var dayPresentation = CalendarPeriodPresentation.Create(
    weekState.ChangeViewMode(CalendarViewMode.Day),
    repository,
    new DateOnly(2026, 8, 10));
Assert(
    dayPresentation.Days.Count == 1 && dayPresentation.Days[0].Events.Select(item => item.EventId).SequenceEqual(["all-day", "standup", "lunch", "review"]),
    "Day presentation must order all-day events before timed events and preserve stable ordering.");

var agendaPresentation = CalendarPeriodPresentation.Create(
    weekState.ChangeViewMode(CalendarViewMode.Agenda),
    repository,
    new DateOnly(2026, 8, 10));
Assert(
    agendaPresentation.RangeStart == new DateOnly(2026, 8, 1) &&
    agendaPresentation.RangeEndExclusive == new DateOnly(2026, 9, 1) &&
    agendaPresentation.Days.Count == 1 &&
    agendaPresentation.Days[0].Date == new DateOnly(2026, 8, 10),
    "Agenda presentation must group non-empty days in the selected visible month.");
Assert(
    CalendarPeriodPresentation.Create(
        CalendarUiState.Create(new DateOnly(2027, 1, 4)).ChangeViewMode(CalendarViewMode.Agenda),
        repository,
        new DateOnly(2026, 8, 10)).IsEmpty,
    "Agenda presentation must expose an explicit empty state for a month without events.");

var a2UiEvent = CalendarEvent.Create(
    "event-a2ui",
    "A2UI event",
    day.AddHours(9),
    day.AddHours(10),
    "Presentation note",
    "Studio");
var a2UiPresentation = CalendarA2UiPresentations.Create(a2UiEvent);
using var a2UiTemplate = JsonDocument.Parse(a2UiPresentation.Template);
using var a2UiDocument = JsonDocument.Parse(a2UiPresentation.Document);
Assert(
    a2UiTemplate.RootElement.GetProperty("surfaceUpdate").GetProperty("surfaceId").GetString() == "calendar-event-card" &&
    a2UiTemplate.RootElement.GetProperty("surfaceUpdate").GetProperty("components").GetArrayLength() == 5,
    "View ToPresentation must return an A2UI surfaceUpdate template as JSON.");
Assert(
    a2UiDocument.RootElement.GetProperty("dataModelUpdate").GetProperty("value").GetProperty("id").GetString() == "event-a2ui" &&
    a2UiDocument.RootElement.GetProperty("dataModelUpdate").GetProperty("value").GetProperty("title").GetString() == "A2UI event",
    "The A2UI dataModelUpdate document must carry the calendar entity values separately from its template.");

var annotations = CalendarViewAnnotationRegistry.Create(
[
    CalendarEvent.Create("event-annotated", "Annotated event", day.AddHours(9), day.AddHours(10), "Agent context", "Studio"),
], calendarEvent => $"generated:{calendarEvent.Id}");
Assert(
    annotations.Count == 1 &&
    annotations[0].EntityType == "Tizen.Entity.Calendar" &&
    annotations[0].EntityId == "event-annotated" &&
    annotations[0].EntityJson == "generated:event-annotated",
    "Each published calendar view annotation must retain stable EntityType/EntityId hints and the generated Entity ToJson snapshot.");

var emptyAnnotations = CalendarViewAnnotationRegistry.Create([], calendarEvent => $"generated:{calendarEvent.Id}");
Assert(emptyAnnotations.Count == 0, "No Calendar Event must produce no published view annotation.");

var theme = CalendarTheme.Light;
Assert(theme.MonthPaneRatio == 0.68f && theme.AgendaPaneRatio == 0.32f, "Candidate B must preserve the approved 68:32 split.");
Assert(theme.SafeInsetHorizontal == 64 && theme.SafeInsetVertical == 44, "The reference canvas must preserve TV-safe insets.");
Assert(theme.FocusOutlineWidth >= 3 && theme.FocusScale > 1.0f, "Focus must use a non-color outline and scale cue.");
Assert(
    !string.IsNullOrWhiteSpace(theme.RootSurface) &&
    !string.IsNullOrWhiteSpace(theme.CellSelectedSurface) &&
    !string.IsNullOrWhiteSpace(theme.TodayPillSurface) &&
    theme.EventColors.Count == 4,
    "The light theme must provide named root, selected, today, and four event-color tokens.");

var touchSelectedDate = new DateOnly(2026, 8, 23);
state = CalendarUiReducer.Reduce(
    CalendarUiState.Create(new DateOnly(2026, 8, 10)),
    new CalendarUiCommand.SelectDate(touchSelectedDate),
    today: new DateOnly(2026, 8, 10),
    selectedDateEventCount: 0);
Assert(
    state.SelectedDate == touchSelectedDate &&
    state.VisibleMonth == new DateOnly(2026, 8, 1) &&
    state.FocusRegion == CalendarFocusRegion.MonthGrid,
    "Touch date selection must dispatch the same semantic selection transition as remote navigation.");

var touchActivation = new CalendarTouchActivation();
Assert(!touchActivation.PointerUp(isInside: true), "Pointer Up without a matching Down must not activate.");
touchActivation.PointerDown();
Assert(!touchActivation.PointerUp(isInside: false), "Dragging outside before pointer Up must cancel activation.");
touchActivation.PointerDown();
Assert(touchActivation.PointerUp(isInside: true), "Pointer Down followed by Up-inside must activate exactly once.");
Assert(!touchActivation.PointerUp(isInside: true), "A consumed pointer sequence must not activate twice.");

state = CalendarUiReducer.Reduce(
    CalendarUiState.Create(new DateOnly(2026, 8, 10)),
    new CalendarUiCommand.SelectAgendaEvent(1),
    today: new DateOnly(2026, 8, 10),
    selectedDateEventCount: 2);
Assert(
    state.IsAgendaOpen && state.FocusRegion == CalendarFocusRegion.AgendaEvents && state.FocusedAgendaIndex == 1,
    "Touching an agenda card must enter the agenda and focus that exact event.");

state = CalendarUiReducer.Reduce(
    CalendarUiState.Create(new DateOnly(2026, 8, 10)).FocusTodayControl(),
    new CalendarUiCommand.ActivateToday(),
    today: new DateOnly(2026, 9, 7),
    selectedDateEventCount: 0);
Assert(
    state.SelectedDate == new DateOnly(2026, 9, 7) && state.FocusRegion == CalendarFocusRegion.Today,
    "Touching Today must use the same semantic transition as remote activation.");

var editor = CalendarEditorState.CreateNew(new DateOnly(2026, 8, 23));
var expectedEditorStart = CalendarDateBoundary.AtStartOfDay(new DateOnly(2026, 8, 23)).AddHours(9);
Assert(
    editor.Start == expectedEditorStart &&
    editor.End == expectedEditorStart.AddHours(1) &&
    !editor.CanSave,
    "A new event editor must prefill a one-hour range on the selected date and require a title.");
editor = editor.WithTitle("Project review");
Assert(editor.CanSave, "A valid title and positive time range must enable Save.");
editor = editor.WithRange(editor.Start, editor.Start);
Assert(!editor.CanSave && editor.ValidationMessage == "End time must be after start time.", "An invalid range must disable Save and expose inline validation.");

editor = CalendarEditorState.CreateNew(new DateOnly(2026, 8, 23)).ToggleReminder(30);
Assert(editor.ReminderOffsets.SetEquals([30]), "Selecting a reminder preset must add the exact offset.");
editor = editor.ToggleReminder(30);
Assert(editor.ReminderOffsets.Count == 0, "Selecting an active reminder preset must remove it.");

var interaction = CalendarInteractionState.Create(CalendarUiState.Create(new DateOnly(2026, 8, 23))).OpenNewEvent();
Assert(
    interaction.Surface == CalendarSurface.EventEditor &&
    interaction.EventEditor is not null &&
    interaction.EventEditor.Start.Date == new DateTime(2026, 8, 23),
    "Opening Add must create an editor for the selected calendar date without losing calendar state.");
interaction = interaction.Back();
Assert(
    interaction.Surface == CalendarSurface.Calendar && interaction.Calendar.SelectedDate == new DateOnly(2026, 8, 23),
    "Back from a new-event editor must close the editor and restore the selected date.");

interaction = CalendarInteractionState.Create(CalendarUiState.Create(new DateOnly(2026, 8, 23)))
    .OpenEventDetail("event-review");
Assert(
    interaction.Surface == CalendarSurface.EventDetail && interaction.SelectedEventId == "event-review",
    "Selecting an event must open detail for its stable ID.");
var existingEditorEvent = CalendarEvent.Create(
    "event-review",
    "Design review",
    day.AddHours(15),
    day.AddHours(16),
    string.Empty,
    "Meeting room");
interaction = interaction.OpenEventEditor(existingEditorEvent, [30]);
Assert(
    interaction.Surface == CalendarSurface.EventEditor &&
    interaction.EventEditor is { EventId: "event-review", Title: "Design review" } &&
    interaction.EventEditor.ReminderOffsets.SetEquals([30]),
    "Edit from detail must preserve the stable event ID and prefill persisted fields and reminder offsets.");
interaction = interaction.Back();
Assert(interaction.Surface == CalendarSurface.EventDetail, "Back from editing an existing event must restore its detail.");
interaction = interaction.RequestEventDelete();
Assert(
    interaction.Surface == CalendarSurface.DeleteEventConfirmation && interaction.SelectedEventId == "event-review",
    "Delete must enter confirmation without losing the selected stable ID.");
interaction = interaction.CancelEventDelete();
Assert(
    interaction.Surface == CalendarSurface.EventDetail && interaction.SelectedEventId == "event-review",
    "Cancelling destructive confirmation must restore the exact event detail.");
interaction = CalendarInteractionState.Create(CalendarUiState.Create(new DateOnly(2026, 8, 23))).OpenReminderList();
Assert(interaction.Surface == CalendarSurface.ReminderList, "Opening Reminders must preserve calendar state and enter the independent reminder list.");
var reminderDue = new DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);
interaction = interaction.OpenNewReminder(reminderDue);
Assert(interaction.Surface == CalendarSurface.ReminderEditor && interaction.ReminderEditor is { CanSave: false, DueAt: var due } && due == reminderDue, "Add reminder must open a validated editor with the suggested due time.");
interaction = interaction with { ReminderEditor = interaction.ReminderEditor!.WithTitle("Buy milk") };
Assert(interaction.ReminderEditor.CanSave, "Reminder title and future due date must enable Save.");
interaction = interaction.Back();
Assert(interaction.Surface == CalendarSurface.ReminderList, "Back from reminder editor must restore the reminder list.");
interaction = interaction.Back();
Assert(interaction.Surface == CalendarSurface.Calendar, "Back from the reminder list must restore the calendar.");

interaction = CalendarInteractionState.Create(CalendarUiState.Create(new DateOnly(2026, 8, 23))).OpenSearch();
Assert(
    interaction.Surface == CalendarSurface.Search &&
    interaction.Search is { StartDate: var searchStart, EndDateExclusive: var searchEnd } &&
    searchStart == new DateOnly(2026, 8, 1) && searchEnd == new DateOnly(2026, 9, 1),
    "Opening advanced search must default to the selected visible month.");
var searchState = interaction.Search!
    .WithKeyword("studio")
    .WithFields(searchTitle: false, searchLocation: true, searchNote: false)
    .WithPeriod(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 11))
    .Apply(repository);
Assert(
    searchState.ResultEventIds.SequenceEqual(["standup"]) && searchState.ValidationMessage is null && searchState.HasApplied,
    "Advanced search state must combine selected text fields with a start-inclusive/end-exclusive range.");
var searchDetail = (interaction with { Search = searchState }).OpenSearchResult("standup");
Assert(
    searchDetail.Surface == CalendarSurface.EventDetail && searchDetail.Search is not null && searchDetail.SearchReturnEventId == "standup",
    "Opening a search result must preserve the applied query and exact result focus anchor.");
searchDetail = searchDetail.Back();
Assert(
    searchDetail.Surface == CalendarSurface.Search && searchDetail.Search?.ResultEventIds.SequenceEqual(["standup"]) == true,
    "Back from a search-originated event detail must restore the same result list.");
searchState = searchState.WithPeriod(new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 11));
Assert(!searchState.CanApply && searchState.ValidationMessage is not null, "An inverted UI search period must expose inline validation.");
searchState = searchState.Apply(repository);
Assert(!searchState.HasApplied && searchState.ResultEventIds.Count == 0, "Invalid search criteria must never retain stale applied results.");
interaction = (interaction with { Search = searchState }).Back();
Assert(
    interaction.Surface == CalendarSurface.Calendar && interaction.Search is null && interaction.Calendar.FocusRegion == CalendarFocusRegion.Search,
    "Back from advanced search must restore calendar context and the Search command-bar focus anchor.");

state = CalendarUiState.Create(new DateOnly(2026, 8, 10)).EnterAgenda(eventCount: 2);
state = state.MoveAgendaFocus(1, eventCount: 2);
state = state.MoveAgendaFocus(1, eventCount: 2);
Assert(state.FocusRegion == CalendarFocusRegion.AgendaAdd, "Agenda Down from the final event must focus Add event.");
state = state.MoveAgendaFocus(1, eventCount: 2);
Assert(state.FocusRegion == CalendarFocusRegion.AgendaReminders, "Agenda Down from Add event must focus independent Reminders.");
state = state.MoveAgendaFocus(-1, eventCount: 2);
Assert(state.FocusRegion == CalendarFocusRegion.AgendaAdd, "Agenda Up from Reminders must restore Add event.");
state = state.MoveAgendaFocus(-1, eventCount: 2);
Assert(state.FocusRegion == CalendarFocusRegion.AgendaEvents && state.FocusedAgendaIndex == 1, "Agenda Up from Add must restore the final event card.");

var periodFocusState = CalendarUiState.Create(new DateOnly(2026, 8, 10))
    .ChangeViewMode(CalendarViewMode.Week)
    .FocusPeriodEvent("standup");
Assert(
    periodFocusState.FocusRegion == CalendarFocusRegion.PeriodEvents && periodFocusState.FocusedEventId == "standup",
    "Period views must retain the stable ID of the logically focused event card.");
var pointerOpenedPeriodState = CalendarUiReducer.Reduce(
    periodFocusState.FocusHeader(CalendarFocusRegion.WeekMode),
    new CalendarUiCommand.OpenEvent("review"),
    today: new DateOnly(2026, 8, 10),
    selectedDateEventCount: 0);
Assert(
    pointerOpenedPeriodState.FocusRegion == CalendarFocusRegion.PeriodEvents && pointerOpenedPeriodState.FocusedEventId == "review",
    "Pointer activation must preserve the clicked period event as the logical Back-focus anchor.");
periodFocusState = periodFocusState.MovePeriod(1);
Assert(
    periodFocusState.FocusRegion == CalendarFocusRegion.PeriodEmptyState && periodFocusState.FocusedEventId is null,
    "Period movement from content must clear stale event focus and select a renderable content anchor.");

var staleAgendaFocus = CalendarUiState.Create(new DateOnly(2026, 8, 10)).EnterAgenda(2).MovePeriod(1);
Assert(
    staleAgendaFocus.FocusRegion == CalendarFocusRegion.MonthGrid && staleAgendaFocus.FocusedAgendaIndex is null,
    "Month movement must not retain an agenda focus region after closing the agenda.");

var renderPolicyDay = CalendarDateBoundary.AtStartOfDay(new DateOnly(2026, 8, 10));
var renderPolicyRepository = new CalendarEventRepository(
    Enumerable.Range(0, CalendarPeriodRenderPolicy.WeekEventsPerDay + 2)
        .Select(index => CalendarEvent.Create(
            $"render-{index}",
            $"Rendered event {index}",
            renderPolicyDay.AddHours(index + 1),
            renderPolicyDay.AddHours(index + 2),
            string.Empty,
            string.Empty)));
var renderPolicyState = CalendarUiState.Create(new DateOnly(2026, 8, 10)).ChangeViewMode(CalendarViewMode.Week);
var renderPolicyPresentation = CalendarPeriodPresentation.Create(renderPolicyState, renderPolicyRepository, new DateOnly(2026, 8, 10));
Assert(
    CalendarPeriodRenderPolicy.GetRenderedEventIds(renderPolicyPresentation).Count == CalendarPeriodRenderPolicy.WeekEventsPerDay,
    "Period D-pad navigation must use exactly the event IDs for cards that the renderer creates.");

var dayPolicyState = renderPolicyState.ChangeViewMode(CalendarViewMode.Day);
var dayPolicyPresentation = CalendarPeriodPresentation.Create(dayPolicyState, renderPolicyRepository, new DateOnly(2026, 8, 10));
Assert(
    CalendarPeriodRenderPolicy.GetRenderedEventIds(dayPolicyPresentation).Count == CalendarPeriodRenderPolicy.DayEvents,
    "Day D-pad navigation must exclude event cards beyond the renderer limit.");

var agendaPolicyRepository = new CalendarEventRepository(
    Enumerable.Range(0, CalendarPeriodRenderPolicy.AgendaDays)
        .Select(index =>
        {
            var start = renderPolicyDay.AddDays(index).AddHours(10);
            return CalendarEvent.Create($"agenda-render-{index}", $"Agenda event {index}", start, start.AddHours(1), string.Empty, string.Empty);
        }));
var agendaPolicyState = CalendarUiState.Create(new DateOnly(2026, 8, 10)).ChangeViewMode(CalendarViewMode.Agenda);
var agendaPolicyPresentation = CalendarPeriodPresentation.Create(agendaPolicyState, agendaPolicyRepository, new DateOnly(2026, 8, 10));
Assert(
    CalendarPeriodRenderPolicy.GetRenderedEventIds(agendaPolicyPresentation, agendaHeight: 596.0f).Count == 7,
    "Agenda D-pad navigation must use the same height-dependent day count as the renderer at 1280x720.");

var existingEvent = repository.ResolveByIds(["review"]).Events.Single();
editor = CalendarEditorState.CreateExisting(existingEvent, [10, 60]);
Assert(
    editor.IsEditing && editor.EventId == "review" && editor.Title == "Design review" && editor.ReminderOffsets.SetEquals([10, 60]),
    "Editing must preserve the stable event ID, fields, and linked reminder presets.");

Console.WriteLine("Calendar.App.Tests: PASS");
