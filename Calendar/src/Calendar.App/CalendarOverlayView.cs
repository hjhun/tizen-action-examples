using System.Globalization;
using Calendar.Domain;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NuiButton = Tizen.NUI.Components.Button;

namespace Calendar.App;

internal static class CalendarOverlayView
{
    public static View Create(
        CalendarInteractionState interaction,
        CalendarEventRepository repository,
        CalendarReminderRepository reminderRepository,
        Action close,
        Action<CalendarEditorState> saveEvent,
        Action editEvent,
        Action requestDelete,
        Action confirmDelete,
        Action cancelDelete,
        Action openNewReminder,
        Action<string> editReminder,
        Action<CalendarReminderEditorState> saveReminder,
        Action requestReminderDelete,
        Action confirmReminderDelete,
        Action<string> toggleReminderCompletion,
        Action<CalendarSearchState> applySearch,
        Action<string> openSearchResult)
    {
        var windowSize = Window.Default.WindowSize;
        var theme = CalendarTheme.Light;
        var scale = Math.Min(windowSize.Width / 1920.0f, windowSize.Height / 1080.0f);
        var paneWidth = 760.0f * scale;
        var root = new View
        {
            Name = "CalendarOverlay",
            Size = new Size(windowSize.Width, windowSize.Height),
            BackgroundColor = new Color("#66000000"),
            Focusable = true,
            FocusableChildren = true,
        };
        var pane = new View
        {
            Name = "CalendarOverlayPane",
            Position = new Position(windowSize.Width - paneWidth, 0.0f),
            Size = new Size(paneWidth, windowSize.Height),
            BackgroundColor = new Color(theme.SecondarySurface),
            FocusableChildren = true,
        };
        root.Add(pane);

        var closeButton = CreateButton(
            "Close",
            new Position(36.0f * scale, 28.0f * scale),
            new Size(130.0f * scale, 58.0f * scale),
            close);
        closeButton.Name = "CalendarOverlayClose";
        pane.Add(closeButton);

        switch (interaction.Surface)
        {
            case CalendarSurface.EventEditor:
                AddEventEditor(pane, interaction.EventEditor!, theme, scale, saveEvent, close);
                break;
            case CalendarSurface.EventDetail:
                AddEventDetail(pane, interaction, repository, theme, scale, editEvent, requestDelete);
                break;
            case CalendarSurface.DeleteEventConfirmation:
                AddDeleteConfirmation(pane, interaction, repository, theme, scale, confirmDelete, cancelDelete);
                break;
            case CalendarSurface.ReminderList:
                AddReminderList(pane, reminderRepository, theme, scale, openNewReminder, editReminder, toggleReminderCompletion);
                break;
            case CalendarSurface.ReminderEditor:
                AddReminderEditor(pane, interaction.ReminderEditor!, theme, scale, saveReminder, close, requestReminderDelete);
                break;
            case CalendarSurface.DeleteReminderConfirmation:
                AddReminderDeleteConfirmation(pane, interaction, reminderRepository, theme, scale, confirmReminderDelete, close);
                break;
            case CalendarSurface.Search:
                AddSearch(pane, interaction.Search!, repository, theme, scale, applySearch, openSearchResult);
                break;
            default:
                pane.Add(CalendarDateCellView.CreateLabel(
                    interaction.Surface.ToString(),
                    theme.TextPrimary,
                    7.0f * scale,
                    new Position(48.0f * scale, 125.0f * scale),
                    new Size(paneWidth - (96.0f * scale), 80.0f * scale),
                    HorizontalAlignment.Begin));
                break;
        }

        root.RaiseToTop();
        return root;
    }

    private static void AddEventEditor(
        View pane,
        CalendarEditorState editor,
        CalendarTheme theme,
        float scale,
        Action<CalendarEditorState> saveEvent,
        Action cancel)
    {
        var working = editor;
        pane.Add(CalendarDateCellView.CreateLabel(
            editor.IsEditing ? "Edit event" : "Add event",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 105.0f * scale),
            new Size(620.0f * scale, 72.0f * scale),
            HorizontalAlignment.Begin));

        var title = CreateTextField(editor.Title, "Title", 48, 190, 650, 66, scale);
        var date = CreateTextField(editor.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "YYYY-MM-DD", 48, 278, 300, 62, scale);
        var start = CreateTextField(editor.Start.ToString("HH:mm", CultureInfo.InvariantCulture), "Start HH:mm", 370, 278, 150, 62, scale);
        var end = CreateTextField(editor.End.ToString("HH:mm", CultureInfo.InvariantCulture), "End HH:mm", 548, 278, 150, 62, scale);
        var location = CreateTextField(editor.Location, "Location (optional)", 48, 365, 650, 62, scale);
        var note = new TextEditor
        {
            Name = "EventNoteEditor",
            Text = editor.Note,
            PlaceholderText = "Note (optional)",
            PlaceholderTextColor = new Color(0.58f, 0.58f, 0.62f, 1.0f),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(48.0f * scale, 448.0f * scale),
            Size = new Size(650.0f * scale, 120.0f * scale),
            BackgroundColor = new Color(theme.CellSurface),
        };
        title.TextChanged += (_, args) => working = working.WithTitle(args.TextField.Text);
        location.TextChanged += (_, args) => working = working with { Location = args.TextField.Text };
        note.TextChanged += (_, args) => working = working with { Note = args.TextEditor.Text };
        pane.Add(title);
        pane.Add(date);
        pane.Add(start);
        pane.Add(end);
        pane.Add(location);
        pane.Add(note);

        pane.Add(CalendarDateCellView.CreateLabel(
            "Reminder",
            theme.TextSecondary,
            3.2f * scale,
            new Position(48.0f * scale, 590.0f * scale),
            new Size(200.0f * scale, 40.0f * scale),
            HorizontalAlignment.Begin));

        var presets = new[] { (10, "10 min"), (30, "30 min"), (60, "1 hour"), (1440, "1 day") };
        for (var index = 0; index < presets.Length; index++)
        {
            var preset = presets[index];
            var chip = CreateButton(
                preset.Item2,
                new Position((48.0f + (index * 158.0f)) * scale, 638.0f * scale),
                new Size(142.0f * scale, 58.0f * scale),
                () => working = working.ToggleReminder(preset.Item1),
                selectable: true,
                selected: editor.ReminderOffsets.Contains(preset.Item1));
            pane.Add(chip);
        }

        var validation = CalendarDateCellView.CreateLabel(
            string.Empty,
            "#C62828",
            3.0f * scale,
            new Position(48.0f * scale, 722.0f * scale),
            new Size(650.0f * scale, 42.0f * scale),
            HorizontalAlignment.Begin);
        pane.Add(validation);

        pane.Add(CreateButton(
            "Cancel",
            new Position(342.0f * scale, 796.0f * scale),
            new Size(168.0f * scale, 66.0f * scale),
            cancel));
        pane.Add(CreateButton(
            "Save",
            new Position(530.0f * scale, 796.0f * scale),
            new Size(168.0f * scale, 66.0f * scale),
            () =>
            {
                if (!DateTime.TryParseExact(date.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ||
                    !TimeSpan.TryParseExact(start.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var parsedStart) ||
                    !TimeSpan.TryParseExact(end.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var parsedEnd))
                {
                    validation.Text = "Enter a valid date and 24-hour time.";
                    return;
                }

                var offset = editor.Start.Offset;
                working = working.WithRange(
                    new DateTimeOffset(parsedDate.Date.Add(parsedStart), offset),
                    new DateTimeOffset(parsedDate.Date.Add(parsedEnd), offset));
                if (!working.CanSave)
                {
                    validation.Text = working.ValidationMessage ?? "Check the event details.";
                    return;
                }

                title.KeyInputFocus = false;
                title.GetInputMethodContext()?.HideInputPanel();
                saveEvent(working);
            }));
    }

    private static void AddEventDetail(
        View pane,
        CalendarInteractionState interaction,
        CalendarEventRepository repository,
        CalendarTheme theme,
        float scale,
        Action edit,
        Action requestDelete)
    {
        var calendarEvent = ResolveEvent(interaction, repository);
        pane.Add(CalendarDateCellView.CreateLabel(
            calendarEvent?.Title ?? "Event not found",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 135.0f * scale),
            new Size(650.0f * scale, 95.0f * scale),
            HorizontalAlignment.Begin));
        if (calendarEvent is null)
        {
            return;
        }

        AddField(pane, "When", $"{calendarEvent.Start:ddd, MMM d  HH:mm} – {calendarEvent.End:HH:mm}", 270.0f, theme, scale);
        AddField(pane, "Location", string.IsNullOrWhiteSpace(calendarEvent.Location) ? "—" : calendarEvent.Location, 405.0f, theme, scale);
        AddField(pane, "Note", string.IsNullOrWhiteSpace(calendarEvent.Note) ? "—" : calendarEvent.Note, 540.0f, theme, scale);
        pane.Add(CreateButton("Edit", new Position(342.0f * scale, 760.0f * scale), new Size(168.0f * scale, 66.0f * scale), edit));
        pane.Add(CreateButton("Delete", new Position(530.0f * scale, 760.0f * scale), new Size(168.0f * scale, 66.0f * scale), requestDelete));
    }

    private static void AddDeleteConfirmation(
        View pane,
        CalendarInteractionState interaction,
        CalendarEventRepository repository,
        CalendarTheme theme,
        float scale,
        Action confirmDelete,
        Action cancelDelete)
    {
        var calendarEvent = ResolveEvent(interaction, repository);
        pane.Add(CalendarDateCellView.CreateLabel(
            "Delete event?",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 220.0f * scale),
            new Size(650.0f * scale, 80.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            calendarEvent is null ? "Event not found" : $"{calendarEvent.Title}\n{calendarEvent.Start:ddd, MMM d, yyyy  HH:mm}",
            theme.TextSecondary,
            4.0f * scale,
            new Position(48.0f * scale, 325.0f * scale),
            new Size(650.0f * scale, 150.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CreateButton("Cancel", new Position(342.0f * scale, 565.0f * scale), new Size(168.0f * scale, 66.0f * scale), cancelDelete));
        pane.Add(CreateButton("Delete", new Position(530.0f * scale, 565.0f * scale), new Size(168.0f * scale, 66.0f * scale), confirmDelete));
    }

    private static void AddReminderList(
        View pane,
        CalendarReminderRepository repository,
        CalendarTheme theme,
        float scale,
        Action openNew,
        Action<string> edit,
        Action<string> toggleCompletion)
    {
        pane.Add(CalendarDateCellView.CreateLabel(
            "Reminders",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 110.0f * scale),
            new Size(440.0f * scale, 72.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CreateButton("Add reminder", new Position(500.0f * scale, 112.0f * scale), new Size(198.0f * scale, 60.0f * scale), openNew));

        var reminders = repository.Snapshot()
            .Where(reminder => reminder.CalendarEventId is null)
            .OrderBy(reminder => reminder.IsCompleted)
            .ThenBy(reminder => reminder.DueAt)
            .Take(7)
            .ToArray();
        if (reminders.Length == 0)
        {
            pane.Add(CalendarDateCellView.CreateLabel(
                "No reminders\nAdd one for a task that is not tied to an event.",
                theme.TextSecondary,
                4.2f * scale,
                new Position(48.0f * scale, 275.0f * scale),
                new Size(650.0f * scale, 150.0f * scale),
                HorizontalAlignment.Center));
            return;
        }

        for (var index = 0; index < reminders.Length; index++)
        {
            var reminder = reminders[index];
            var top = (205.0f + (index * 100.0f)) * scale;
            var card = CreateButton(
                $"{(reminder.IsCompleted ? "✓" : "○")}  {reminder.Title}    {reminder.DueAt:MMM d  HH:mm}",
                new Position(48.0f * scale, top),
                new Size(545.0f * scale, 82.0f * scale),
                () => edit(reminder.Id));
            pane.Add(card);
            pane.Add(CreateButton(
                reminder.IsCompleted ? "Reopen" : "Done",
                new Position(608.0f * scale, top),
                new Size(90.0f * scale, 82.0f * scale),
                () => toggleCompletion(reminder.Id)));
        }
    }

    private static void AddReminderEditor(
        View pane,
        CalendarReminderEditorState editor,
        CalendarTheme theme,
        float scale,
        Action<CalendarReminderEditorState> save,
        Action cancel,
        Action requestDelete)
    {
        var working = editor;
        pane.Add(CalendarDateCellView.CreateLabel(
            editor.IsEditing ? "Edit reminder" : "Add reminder",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 110.0f * scale),
            new Size(620.0f * scale, 72.0f * scale),
            HorizontalAlignment.Begin));
        var title = CreateTextField(editor.Title, "Title", 48, 205, 650, 66, scale);
        var date = CreateTextField(editor.DueAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "YYYY-MM-DD", 48, 300, 360, 62, scale);
        var time = CreateTextField(editor.DueAt.ToString("HH:mm", CultureInfo.InvariantCulture), "HH:mm", 430, 300, 268, 62, scale);
        var note = new TextEditor
        {
            Text = editor.Note,
            PlaceholderText = "Note (optional)",
            PlaceholderTextColor = new Color(0.58f, 0.58f, 0.62f, 1.0f),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(48.0f * scale, 395.0f * scale),
            Size = new Size(650.0f * scale, 150.0f * scale),
            BackgroundColor = new Color(theme.CellSurface),
        };
        title.TextChanged += (_, args) => working = working.WithTitle(args.TextField.Text);
        note.TextChanged += (_, args) => working = working with { Note = args.TextEditor.Text };
        pane.Add(title);
        pane.Add(date);
        pane.Add(time);
        pane.Add(note);

        var validation = CalendarDateCellView.CreateLabel(string.Empty, "#C62828", 3.0f * scale, new Position(48.0f * scale, 570.0f * scale), new Size(650.0f * scale, 42.0f * scale), HorizontalAlignment.Begin);
        pane.Add(validation);
        if (editor.IsEditing)
        {
            pane.Add(CreateButton("Delete", new Position(48.0f * scale, 690.0f * scale), new Size(170.0f * scale, 66.0f * scale), requestDelete));
        }

        pane.Add(CreateButton("Cancel", new Position(342.0f * scale, 690.0f * scale), new Size(168.0f * scale, 66.0f * scale), cancel));
        pane.Add(CreateButton(
            "Save",
            new Position(530.0f * scale, 690.0f * scale),
            new Size(168.0f * scale, 66.0f * scale),
            () =>
            {
                if (!DateTime.TryParseExact(date.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ||
                    !TimeSpan.TryParseExact(time.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var parsedTime))
                {
                    validation.Text = "Enter a valid date and 24-hour time.";
                    return;
                }

                working = working with
                {
                    DueAt = new DateTimeOffset(parsedDate.Date.Add(parsedTime), editor.DueAt.Offset),
                };
                if (!working.CanSave)
                {
                    validation.Text = working.ValidationMessage ?? "Check the reminder details.";
                    return;
                }

                save(working);
            }));
    }

    private static void AddReminderDeleteConfirmation(
        View pane,
        CalendarInteractionState interaction,
        CalendarReminderRepository repository,
        CalendarTheme theme,
        float scale,
        Action confirm,
        Action cancel)
    {
        var reminder = interaction.SelectedReminderId is null ? null : repository.Find(interaction.SelectedReminderId);
        pane.Add(CalendarDateCellView.CreateLabel("Delete reminder?", theme.TextPrimary, 8.0f * scale, new Position(48.0f * scale, 220.0f * scale), new Size(650.0f * scale, 80.0f * scale), HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            reminder is null ? "Reminder not found" : $"{reminder.Title}\n{reminder.DueAt:ddd, MMM d, yyyy  HH:mm}",
            theme.TextSecondary,
            4.0f * scale,
            new Position(48.0f * scale, 325.0f * scale),
            new Size(650.0f * scale, 150.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CreateButton("Cancel", new Position(342.0f * scale, 565.0f * scale), new Size(168.0f * scale, 66.0f * scale), cancel));
        pane.Add(CreateButton("Delete", new Position(530.0f * scale, 565.0f * scale), new Size(168.0f * scale, 66.0f * scale), confirm));
    }

    private static void AddSearch(
        View pane,
        CalendarSearchState search,
        CalendarEventRepository repository,
        CalendarTheme theme,
        float scale,
        Action<CalendarSearchState> apply,
        Action<string> openResult)
    {
        var working = search;
        pane.Add(CalendarDateCellView.CreateLabel(
            "Advanced search",
            theme.TextPrimary,
            8.0f * scale,
            new Position(48.0f * scale, 105.0f * scale),
            new Size(620.0f * scale, 72.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            "Title, location, or note",
            theme.TextSecondary,
            3.0f * scale,
            new Position(48.0f * scale, 174.0f * scale),
            new Size(620.0f * scale, 34.0f * scale),
            HorizontalAlignment.Begin));

        var keyword = CreateTextField(search.Keyword, "Search events", 48, 214, 650, 66, scale);
        keyword.Name = "CalendarSearchKeyword";
        keyword.AccessibilityName = "Search calendar event title, location, or note";
        pane.Add(CalendarDateCellView.CreateLabel(
            "Start date (inclusive)",
            theme.TextSecondary,
            2.7f * scale,
            new Position(48.0f * scale, 280.0f * scale),
            new Size(305.0f * scale, 24.0f * scale),
            HorizontalAlignment.Begin));
        pane.Add(CalendarDateCellView.CreateLabel(
            "End date (exclusive)",
            theme.TextSecondary,
            2.7f * scale,
            new Position(393.0f * scale, 280.0f * scale),
            new Size(305.0f * scale, 24.0f * scale),
            HorizontalAlignment.Begin));
        var start = CreateTextField(search.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Start YYYY-MM-DD", 48, 304, 305, 62, scale);
        start.Name = "CalendarSearchStartDate";
        start.AccessibilityName = "Search start date";
        var end = CreateTextField(search.EndDateExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Exclusive end YYYY-MM-DD", 393, 304, 305, 62, scale);
        end.Name = "CalendarSearchEndDate";
        end.AccessibilityName = "Search end date exclusive";
        keyword.TextChanged += (_, args) => working = working.WithKeyword(args.TextField.Text);
        pane.Add(keyword);
        pane.Add(start);
        pane.Add(end);

        NuiButton titleField = null!;
        NuiButton locationField = null!;
        NuiButton noteField = null!;
        titleField = CreateButton(
            search.SearchTitle ? "Title ✓" : "Title",
            new Position(48.0f * scale, 382.0f * scale),
            new Size(190.0f * scale, 54.0f * scale),
            () =>
            {
                working = working.WithFields(!working.SearchTitle, working.SearchLocation, working.SearchNote);
                titleField.Text = working.SearchTitle ? "Title ✓" : "Title";
            }, selectable: true, selected: search.SearchTitle);
        titleField.Name = "CalendarSearchTitleField";
        titleField.AccessibilityName = "Search title field";
        locationField = CreateButton(
            search.SearchLocation ? "Location ✓" : "Location",
            new Position(258.0f * scale, 382.0f * scale),
            new Size(190.0f * scale, 54.0f * scale),
            () =>
            {
                working = working.WithFields(working.SearchTitle, !working.SearchLocation, working.SearchNote);
                locationField.Text = working.SearchLocation ? "Location ✓" : "Location";
            }, selectable: true, selected: search.SearchLocation);
        locationField.Name = "CalendarSearchLocationField";
        locationField.AccessibilityName = "Search location field";
        noteField = CreateButton(
            search.SearchNote ? "Notes ✓" : "Notes",
            new Position(468.0f * scale, 382.0f * scale),
            new Size(190.0f * scale, 54.0f * scale),
            () =>
            {
                working = working.WithFields(working.SearchTitle, working.SearchLocation, !working.SearchNote);
                noteField.Text = working.SearchNote ? "Notes ✓" : "Notes";
            }, selectable: true, selected: search.SearchNote);
        noteField.Name = "CalendarSearchNoteField";
        noteField.AccessibilityName = "Search notes field";
        pane.Add(titleField);
        pane.Add(locationField);
        pane.Add(noteField);

        var validation = CalendarDateCellView.CreateLabel(
            search.ValidationMessage ?? string.Empty,
            "#C62828",
            3.0f * scale,
            new Position(48.0f * scale, 445.0f * scale),
            new Size(650.0f * scale, 38.0f * scale),
            HorizontalAlignment.Begin);
        pane.Add(validation);
        pane.Add(CreateButton(
            "Search",
            new Position(530.0f * scale, 485.0f * scale),
            new Size(168.0f * scale, 62.0f * scale),
            () =>
            {
                if (!DateOnly.TryParseExact(start.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart) ||
                    !DateOnly.TryParseExact(end.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
                {
                    validation.Text = "Enter valid dates in YYYY-MM-DD format.";
                    return;
                }

                working = working.WithKeyword(keyword.Text).WithPeriod(parsedStart, parsedEnd);
                if (!working.CanApply)
                {
                    validation.Text = working.ValidationMessage!;
                    return;
                }

                apply(working);
            }));

        var results = repository.ResolveByIds(search.ResultEventIds).Events;
        if (search.ResultEventIds.Count > 0 && results.Count == 0)
        {
            pane.Add(CalendarDateCellView.CreateLabel("Results changed. Search again.", theme.TextSecondary, 3.5f * scale,
                new Position(48.0f * scale, 560.0f * scale), new Size(650.0f * scale, 50.0f * scale), HorizontalAlignment.Center));
        }
        else if (!search.HasApplied)
        {
            pane.Add(CalendarDateCellView.CreateLabel("Enter filters and choose Search.", theme.TextSecondary, 3.5f * scale,
                new Position(48.0f * scale, 560.0f * scale), new Size(650.0f * scale, 50.0f * scale), HorizontalAlignment.Center));
        }
        else if (search.ResultEventIds.Count == 0)
        {
            pane.Add(CalendarDateCellView.CreateLabel("No events match these filters. Change the dates or keyword and try again.", theme.TextSecondary, 3.5f * scale,
                new Position(48.0f * scale, 560.0f * scale), new Size(650.0f * scale, 100.0f * scale), HorizontalAlignment.Center));
        }
        else
        {
            for (var index = 0; index < Math.Min(4, results.Count); index++)
            {
                var calendarEvent = results[index];
                var resultButton = CreateButton(
                    $"{calendarEvent.Start:MMM d  HH:mm}   {calendarEvent.Title}",
                    new Position(48.0f * scale, (560.0f + (index * 86.0f)) * scale),
                    new Size(650.0f * scale, 70.0f * scale),
                    () => openResult(calendarEvent.Id));
                resultButton.Name = $"CalendarEvent-{calendarEvent.Id}";
                resultButton.AccessibilityName = $"{calendarEvent.Title}, {calendarEvent.Start:MMMM d HH:mm}, {calendarEvent.Location}";
                pane.Add(resultButton);
            }
        }
    }

    private static CalendarEvent? ResolveEvent(CalendarInteractionState interaction, CalendarEventRepository repository) =>
        interaction.SelectedEventId is null
            ? null
            : repository.ResolveByIds([interaction.SelectedEventId]).Events.SingleOrDefault();

    private static TextField CreateTextField(string text, string placeholder, float x, float y, float width, float height, float scale) =>
        new()
        {
            Text = text,
            PlaceholderText = placeholder,
            PlaceholderTextColor = new Vector4(0.58f, 0.58f, 0.62f, 1.0f),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(x * scale, y * scale),
            Size = new Size(width * scale, height * scale),
            BackgroundColor = new Color("#FFFFFFFF"),
        };

    private static void AddField(View pane, string label, string value, float top, CalendarTheme theme, float scale)
    {
        pane.Add(CalendarDateCellView.CreateLabel(label, theme.TextSecondary, 3.2f * scale, new Position(50.0f * scale, top * scale), new Size(180.0f * scale, 34.0f * scale), HorizontalAlignment.Begin));
        var surface = new View
        {
            Position = new Position(48.0f * scale, (top + 38.0f) * scale),
            Size = new Size(650.0f * scale, 66.0f * scale),
            BackgroundColor = new Color(theme.CellSurface),
            CornerRadius = 16.0f * scale,
        };
        surface.Add(CalendarDateCellView.CreateLabel(value, theme.TextPrimary, 3.8f * scale, new Position(18.0f * scale, 0), new Size(614.0f * scale, 66.0f * scale), HorizontalAlignment.Begin));
        pane.Add(surface);
    }

    private static NuiButton CreateButton(
        string text,
        Position position,
        Size size,
        Action action,
        bool selectable = false,
        bool selected = false)
    {
        var button = new NuiButton
        {
            Text = text,
            Position = position,
            Size = size,
            IsSelectable = selectable,
            IsSelected = selected,
            Focusable = true,
        };
        button.Clicked += (_, _) => action();
        return button;
    }
}
