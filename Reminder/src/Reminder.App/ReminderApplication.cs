using ActionExamples.Ui;
using System.Globalization;
using ActionExamples.ViewAnnotations;
using Reminder.Domain;
using Reminder.Persistence;
using Reminder.ScheduleActionProvider;
using Reminder.UseCases;
using Reminder.ViewActionProvider;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NuiButton = Tizen.NUI.Components.Button;

namespace Reminder.App;

internal sealed class ReminderApplication : NUIApplication
{
    private static readonly string[] Navigation = ["Today", "Upcoming", "Overdue", "Completed", "All", "Reservations"];
    private static readonly string[] TimeFilters = ["All", "Morning", "Afternoon", "Evening", "No alert"];
    private ScheduleService? _service;
    private View? _root;
    private View? _activeRoot;
    private string _section = "Today";
    private string? _selectedId;
    private bool _editing;
    private bool _newItem;
    private bool _confirmDelete;
    private ReminderSearchState _search = new();
    private IReadOnlyList<string> _renderedItemIds = [];
    private string _timeFilter = "All";
    private UiChangeDispatcher? _changes;
    private bool _refreshing;
    private Tizen.NUI.Timer? _annotationTimer;
    private bool _paused;
    private ReminderDisplayMetrics? _display;
    private View? _canvas;
    private readonly List<View> _inputFocusViews = [];
    private View? _editorFocus;
    private View? _resumeEditorFocus;

    protected override void OnCreate()
    {
        base.OnCreate();
        _annotationTimer = new Tizen.NUI.Timer(50);
        _annotationTimer.Tick += (_, _) => { PublishAnnotations(); return false; };
        _changes = new UiChangeDispatcher(
            SynchronizationContext.Current ?? new Tizen.Applications.TizenSynchronizationContext(), RefreshFromService);
        var dataPath = Tizen.Applications.Application.Current.DirectoryInfo.Data;
        _service = new ScheduleService(
            new JsonScheduleStore(System.IO.Path.Combine(dataPath, "reminder-data.json")),
            new DeterministicReservationSimulator());
        _service.Changed += _changes.Request;
        ReminderScheduleActionProviderHost.Start(_service);
        ReminderViewActionProviderHost.Start();
        Window.Default.KeyEvent += OnKeyEvent;
        Window.Default.Resized += OnWindowResized;
        Window.Default.Moved += OnWindowMoved;
        Window.Default.InsetsChanged += OnWindowResized;
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        Render();
    }

    protected override void OnPause()
    {
        // A validation-only button click may leave the editor's key input focus intact.
        var currentInput = CurrentEditorInput();
        _resumeEditorFocus = currentInput ?? (IsCurrentEditorInput(_editorFocus) ? _editorFocus : null);
        _paused = true;
        _annotationTimer?.Stop();
        ReminderViewActionProviderHost.ClearPublishedViews();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _paused = false;
        OnWindowResized(this, EventArgs.Empty);
        RefreshFromService();
        var restore = _resumeEditorFocus;
        _resumeEditorFocus = null;
        if (IsCurrentEditorInput(restore)) FocusManager.Instance.SetCurrentFocusView(restore);
        QueueAnnotationRefresh();
    }

    protected override void OnTerminate()
    {
        ReleaseInputFocusTracking();
        if (_service is not null && _changes is not null) _service.Changed -= _changes.Request;
        _changes?.Dispose();
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.Resized -= OnWindowResized;
        Window.Default.Moved -= OnWindowMoved;
        Window.Default.KeyEvent -= OnKeyEvent;
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        _paused = true;
        _activeRoot = null;
        _annotationTimer?.Stop();
        _annotationTimer?.Dispose();
        _annotationTimer = null;
        ReminderViewActionProviderHost.ClearPublishedViews();
        base.OnTerminate();
    }

    private void OnWindowMoved(object? sender, EventArgs args) => QueueAnnotationRefresh();

    private void OnWindowResized(object? sender, EventArgs args)
    {
        if (_paused || !TryReadDisplay(out var display)) return;
        if (_root is null || _canvas is null) { Render(); return; }
        // Keep text edits, caret and focus alive when resolution or insets change.
        _root.Size = new Size(display.WindowWidth, display.WindowHeight);
        _canvas.Position = new Position(display.Viewport.OffsetX, display.Viewport.OffsetY);
        _canvas.Scale = new Vector3(display.Viewport.Scale, display.Viewport.Scale, 1);
        QueueAnnotationRefresh();
    }

    private void RefreshFromService()
    {
        if (_paused || _service is null) return;
        if (_root is null || _canvas is null || _activeRoot is null) { Render(); return; }
        var focused = NuiViewAnnotations.Focused(_activeRoot);
        var focusKey = NuiViewAnnotations.Descendants(_activeRoot).FirstOrDefault(x => x.View == focused).Key;
        var oldIndex = focused is null ? -1 : _renderedItemIds.ToList().FindIndex(id => focused.Name == $"ReminderEntity-{id}");
        _refreshing = true;
        try
        {
            if (!_editing && _selectedId is { } id && !_service.Snapshot.Reminders.Any(x => x.Id == id) &&
                !_service.Snapshot.Reservations.Any(x => x.Id == id))
            { _selectedId = null; _confirmDelete = false; }
            if (_confirmDelete || _activeRoot != _canvas) Render();
            else
            {
                // Replace data rows, keeping the live search field and editor
                // attached so drafts, invalid dates, caret and IME focus survive.
                var list = _canvas.FindChildByName("ReminderListPane");
                var rows = list.FindChildByName("ReminderListItems");
                list.Remove(rows); rows.Dispose();
                var items = GetCurrentItems();
                ((TextLabel)list.FindChildByName("ReminderListCount")).Text = $"{items.Count} items";
                AddListItems(list, items);
                NuiViewAnnotations.Observe(list.FindChildByName("ReminderListItems"), QueueAnnotationRefresh);
                if (!_editing)
                {
                    var detail = _canvas.FindChildByName("ReminderDetailPane");
                    _canvas.Remove(detail); detail.Dispose();
                    AddDetail(_canvas);
                    NuiViewAnnotations.Observe(_canvas.FindChildByName("ReminderDetailPane"), QueueAnnotationRefresh);
                }
            }
            var target = NuiViewAnnotations.Descendants(_activeRoot!).FirstOrDefault(x => x.Key == focusKey).View;
            if (target is not null && target != focused && target.Focusable && target.IsEnabled)
                FocusManager.Instance.SetCurrentFocusView(target);
            else if (target is null)
            {
                if (oldIndex >= 0 && _renderedItemIds.Count > 0)
                    FocusByName($"ReminderEntity-{_renderedItemIds[Math.Min(oldIndex, _renderedItemIds.Count - 1)]}");
                else FocusByName("ReminderSearchApply");
            }
        }
        finally { _refreshing = false; QueueAnnotationRefresh(); }
    }

    private void OnKeyEvent(object? sender, Window.KeyEventArgs args)
    {
        if (args.Key.State != Key.StateType.Down) return;
        var key = args.Key.KeyPressedName;
        if (key is "XF86Back" or "Escape")
        {
            if (_confirmDelete)
            {
                _confirmDelete = false;
                Render();
                FocusByName("ReminderDetailDelete");
            }
            else if (_editing)
            {
                _editing = false;
                _newItem = false;
                Render();
            }
            else Exit();
            return;
        }

        var current = FocusManager.Instance.GetCurrentFocusView();
        var name = current?.Name ?? string.Empty;
        if (_confirmDelete)
        {
            if (key is "Left" or "Up") FocusByName("ReminderDeleteCancel");
            else if (key is "Right" or "Down") FocusByName("ReminderDeleteConfirm");
            return;
        }
        if (_editing)
        {
            // Keep the editor's focus order inside the form; arrows within text remain native.
            if (key is "Tab" || current is not (TextField or TextEditor) && key is "Up" or "Down" or "Left" or "Right")
            {
                ClearEditorFocus();
                var order = new[] { "ReminderEditorTitle", "ReminderEditorDue", "ReminderEditorNote", "ReminderEditorCancel", "ReminderEditorSave" };
                var index = Array.IndexOf(order, name);
                var delta = key is "Up" or "Left" ? -1 : 1;
                FocusByName(order[(Math.Max(index, 0) + delta + order.Length) % order.Length]);
            }
            return;
        }
        if (key == "Down")
        {
            if (name.StartsWith("ReminderNav-", StringComparison.Ordinal))
            {
                var index = Array.IndexOf(Navigation, name["ReminderNav-".Length..]);
                FocusByName($"ReminderNav-{Navigation[Math.Min(Navigation.Length - 1, Math.Max(0, index) + 1)]}");
            }
            else if (name == "ReminderSearchApply")
            {
                if (_section == "Reservations") FocusFirstItem();
                else FocusByName($"ReminderFilter-{_timeFilter.Replace(" ", string.Empty)}");
            }
            else if (name.StartsWith("ReminderFilter-", StringComparison.Ordinal)) FocusFirstItem();
            else if (name.StartsWith("ReminderEntity-", StringComparison.Ordinal)) FocusAdjacentItem(name["ReminderEntity-".Length..], 1);
            else FocusByName($"ReminderNav-{_section}");
        }
        else if (key == "Up")
        {
            if (name.StartsWith("ReminderNav-", StringComparison.Ordinal))
            {
                var index = Array.IndexOf(Navigation, name["ReminderNav-".Length..]);
                FocusByName($"ReminderNav-{Navigation[Math.Max(0, index - 1)]}");
            }
            else if (name.StartsWith("ReminderEntity-", StringComparison.Ordinal)) FocusAdjacentItem(name["ReminderEntity-".Length..], -1);
            else if (name.StartsWith("ReminderFilter-", StringComparison.Ordinal)) FocusByName("ReminderSearchApply");
        }
        else if (key == "Right")
        {
            if (name.StartsWith("ReminderNav-", StringComparison.Ordinal)) FocusByName("ReminderSearchApply");
            else if (name.StartsWith("ReminderFilter-", StringComparison.Ordinal)) FocusAdjacentFilter(name["ReminderFilter-".Length..], 1);
            else if (name.StartsWith("ReminderEntity-", StringComparison.Ordinal))
                FocusByName(_section == "Reservations" ? "ReminderDetailCancelReservation" : "ReminderDetailComplete", "ReminderDetailEdit");
        }
        else if (key == "Left")
        {
            if (name.StartsWith("ReminderFilter-", StringComparison.Ordinal)) FocusAdjacentFilter(name["ReminderFilter-".Length..], -1);
            else if (!name.StartsWith("ReminderNav-", StringComparison.Ordinal)) FocusByName($"ReminderNav-{_section}");
        }
    }

    private void FocusAdjacentFilter(string normalized, int delta)
    {
        var index = Array.FindIndex(TimeFilters, x => x.Replace(" ", string.Empty) == normalized);
        index = Math.Clamp(index + delta, 0, TimeFilters.Length - 1);
        FocusByName($"ReminderFilter-{TimeFilters[index].Replace(" ", string.Empty)}");
    }

    private void FocusFirstItem()
    {
        var first = GetCurrentItems().FirstOrDefault();
        if (first is not null) FocusByName($"ReminderEntity-{first.Id}");
    }

    private void FocusAdjacentItem(string id, int delta)
    {
        var items = GetCurrentItems();
        var index = items.ToList().FindIndex(x => x.Id == id);
        var target = index + delta;
        if (target >= 0 && target < items.Count) FocusByName($"ReminderEntity-{items[target].Id}");
        else if (target < 0) FocusByName($"ReminderFilter-{_timeFilter.Replace(" ", string.Empty)}", "ReminderSearchApply");
    }

    private void FocusByName(params string[] names)
    {
        if (_root is null) return;
        foreach (var name in names)
        {
            var view = _root.FindChildByName(name);
            if (view is null) continue;
            FocusManager.Instance.SetCurrentFocusView(view);
            return;
        }
    }

    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs args) => QueueAnnotationRefresh();

    private bool IsCurrentEditorInput(View? view) => view is not null && _editing && !_confirmDelete &&
        _activeRoot is not null && _activeRoot == _canvas &&
        NuiViewAnnotations.Descendants(_activeRoot).Any(x => x.View == view) &&
        view.IsEnabled && view.Focusable && view.Visibility &&
        view.Name is "ReminderEditorTitle" or "ReminderEditorDue" or "ReminderEditorNote";

    private View? CurrentEditorInput() =>
        _inputFocusViews.FirstOrDefault(view => IsCurrentEditorInput(view) && view.KeyInputFocus);

    private void RememberInputFocus(object? sender, EventArgs args)
    {
        if (_paused || _refreshing) return;
        _editorFocus = sender is View view && IsCurrentEditorInput(view) ? view : null;
    }

    private void TrackInputFocus(View view)
    {
        view.FocusGained += RememberInputFocus;
        _inputFocusViews.Add(view);
    }

    private void ClearEditorFocus() { _editorFocus = null; _resumeEditorFocus = null; }

    private void ReleaseInputFocusTracking()
    {
        ClearEditorFocus();
        foreach (var view in _inputFocusViews) view.FocusGained -= RememberInputFocus;
        _inputFocusViews.Clear();
    }

    private bool TryReadDisplay(out ReminderDisplayMetrics display)
    {
        display = default;
        int screenWidth = 0, screenHeight = 0;
        try
        {
            var width = Tizen.System.Information.TryGetValue("http://tizen.org/feature/screen.width", out screenWidth);
            var height = Tizen.System.Information.TryGetValue("http://tizen.org/feature/screen.height", out screenHeight);
            if (!width || !height) screenWidth = screenHeight = 0;
        }
        catch (Exception exception) { Tizen.Log.Warn("Reminder", $"Screen size unavailable: {exception.Message}"); }
        try
        {
            var size = Window.Default.WindowSize;
            var insets = Window.Default.GetInsets();
            if (!ReminderDisplayMetrics.TryCreate(size.Width, size.Height, screenWidth, screenHeight,
                    insets.Start, insets.Top, insets.End, insets.Bottom, out display)) return false;
            if (_display != display)
            {
                Tizen.Log.Info("Reminder", $"Screen={screenWidth}x{screenHeight}, Window={size.Width}x{size.Height}, " +
                    $"Insets={insets.Start}/{insets.Top}/{insets.End}/{insets.Bottom}, CanvasScale={display.Viewport.Scale}");
                _display = display;
            }
            return true;
        }
        catch (Exception exception)
        {
            Tizen.Log.Warn("Reminder", $"Window geometry unavailable: {exception.Message}");
            return false;
        }
    }

    private void Render()
    {
        if (_service is null || _paused) return;

        if (!TryReadDisplay(out var display)) return;

        ReleaseInputFocusTracking();
        _activeRoot = null;
        ReminderViewActionProviderHost.ClearPublishedViews();
        if (_root is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_root);
            _root.Dispose();
        }

        _root = new View
        {
            Name = "ReminderWorkspace",
            AccessibilityName = "Reminder focused workspace",
            Size = new Size(display.WindowWidth, display.WindowHeight),
            BackgroundColor = new Color("#F7F6FB"),
            FocusableChildren = true,
        };
        _canvas = new View
        {
            Name = "ReminderDesignCanvas",
            ParentOrigin = ParentOrigin.TopLeft, PivotPoint = PivotPoint.TopLeft,
            Size = new Size(ProportionalViewport.ReferenceWidth, ProportionalViewport.ReferenceHeight),
            Position = new Position(display.Viewport.OffsetX, display.Viewport.OffsetY),
            Scale = new Vector3(display.Viewport.Scale, display.Viewport.Scale, 1),
            FocusableChildren = true,
        };
        _root.Add(_canvas);
        AddHeader(_canvas);
        AddNavigation(_canvas);
        AddList(_canvas);
        AddDetail(_canvas);
        if (_confirmDelete) AddDeleteConfirmation(_canvas);
        Window.Default.GetDefaultLayer().Add(_root);
        _activeRoot = _confirmDelete ? _canvas.FindChildByName("ReminderDeleteDialog") : _canvas;
        NuiViewAnnotations.Observe(_root, QueueAnnotationRefresh);

        var preferred = _confirmDelete ? _root.FindChildByName("ReminderDeleteCancel") :
            _editing ? _root.FindChildByName("ReminderEditorTitle") :
            _selectedId is not null ? _root.FindChildByName($"ReminderEntity-{_selectedId}") : null;
        preferred ??= _root.FindChildByName($"ReminderNav-{_section}");
        if (preferred is not null) FocusManager.Instance.SetCurrentFocusView(preferred);
        QueueAnnotationRefresh();
    }

    private void AddHeader(View root)
    {
        root.Add(Label("Reminder", "#201D29", 76f, 58, 28, 550, 84));
        root.Add(Label("Focused workspace · Common Emulator simulator", "#746F7E", 26.4f, 62, 104, 720, 42));
        var add = Button(_section == "Reservations" ? "+ Add simulated reservation" : "+ Add reminder", 1550, 50, 310, 68, OpenNew);
        add.Name = "ReminderAdd";
        add.AccessibilityName = _section == "Reservations" ? "Add simulated reservation" : "Add reminder";
        root.Add(add);
    }

    private void AddNavigation(View root)
    {
        var panel = Surface(50, 164, 300, 850, "#ECE9F3", 28);
        panel.Name = "ReminderNavigation";
        panel.Add(Label("SMART LISTS", "#777181", 22.4f, 28, 24, 240, 40));
        for (var index = 0; index < Navigation.Length; index++)
        {
            var name = Navigation[index];
            var selected = name == _section;
            var button = Button((selected ? "●  " : "○  ") + name, 22, 82 + index * 100, 256, 74, () => SelectSection(name));
            button.Name = $"ReminderNav-{name}";
            button.AccessibilityName = $"{name} smart list{(selected ? ", selected" : string.Empty)}";
            button.BackgroundColor = new Color(selected ? "#DED5F5" : "#F7F6FB");
            button.TextColor = new Color("#292531");
            panel.Add(button);
        }
        panel.Add(Label("Reservations use deterministic\napp-owned simulator jobs.", "#746F7E", 20.8f, 28, 720, 240, 104));
        root.Add(panel);
    }

    private void AddList(View root)
    {
        var panel = Surface(374, 164, 700, 850, "#FFFFFF", 28);
        panel.Name = "ReminderListPane";
        var items = GetCurrentItems();
        panel.Add(Label(_section, "#272330", 49.6f, 34, 25, 430, 65));
        var count = Label($"{items.Count} items", "#777181", 24f, 565, 39, 100, 40);
        count.Name = "ReminderListCount"; panel.Add(count);
        var search = Field(_search.DraftKeyword, "Search title or note", 32, 88, 460, 62);
        search.Name = "ReminderSearch";
        search.AccessibilityName = "Search reminders";
        search.TextChanged += (_, args) => _search = _search.WithDraft(args.TextField.Text);
        panel.Add(search);
        var applySearch = Button("Search", 510, 88, 158, 62, () => { _search = _search.Apply(); Render(); });
        applySearch.Name = "ReminderSearchApply";
        applySearch.AccessibilityName = "Apply reminder search";
        panel.Add(applySearch);
        if (_section != "Reservations")
        {
            for (var filterIndex = 0; filterIndex < TimeFilters.Length; filterIndex++)
            {
                var filter = TimeFilters[filterIndex];
                var chip = Button(filter, 32 + filterIndex * 127, 164, 118, 48, () => { _timeFilter = filter; Render(); });
                chip.Name = $"ReminderFilter-{filter.Replace(" ", string.Empty)}";
                chip.TextLabel.PixelSize = 22;
                chip.AccessibilityName = $"{filter} reminder filter{(filter == _timeFilter ? ", selected" : string.Empty)}";
                chip.BackgroundColor = new Color(filter == _timeFilter ? "#2D2933" : "#F0EDF5");
                chip.TextColor = new Color(filter == _timeFilter ? "#FFFFFF" : "#40394E");
                panel.Add(chip);
            }
        }
        root.Add(panel);

        AddListItems(panel, items);
    }

    private void AddListItems(View panel, IReadOnlyList<ListItem> items)
    {
        var listTop = _section == "Reservations" ? 174 : 228;
        var rows = new View { Name = "ReminderListItems", Position = P(0, listTop),
            Size = S(700, 850 - listTop), FocusableChildren = true };
        panel.Add(rows);
        _renderedItemIds = items.Take(6).Select(item => item.Id).ToArray();
        if (items.Count == 0)
        {
            rows.Add(Label("Nothing here yet", "#40394E", 40f, 80, 280 - listTop, 540, 70, HorizontalAlignment.Center));
            rows.Add(Label(_section == "Reservations" ? "Add a deterministic viewing or recording simulation." : "Choose Add reminder to create your first item.", "#777181", 25.6f, 80, 360 - listTop, 540, 100, HorizontalAlignment.Center));
            return;
        }

        for (var index = 0; index < Math.Min(items.Count, 6); index++)
        {
            var item = items[index];
            var isSelected = item.Id == _selectedId;
            var button = Button(item.Primary + "  ·  " + item.Secondary, 32, index * 96, 636, 80, () => SelectItem(item.Id));
            button.Name = $"ReminderEntity-{item.Id}";
            button.AccessibilityName = item.Primary + ", " + item.Secondary;
            button.BackgroundColor = new Color(isSelected ? "#E6DDF8" : "#F7F6FB");
            button.TextColor = new Color("#292531");
            rows.Add(button);
        }
    }

    private void AddDetail(View root)
    {
        var panel = Surface(1098, 164, 772, 850, "#F0EDF5", 28);
        panel.Name = "ReminderDetailPane";
        if (_editing && _section != "Reservations") AddReminderEditor(panel);
        else if (_section == "Reservations") AddReservationDetail(panel);
        else AddReminderDetail(panel);
        root.Add(panel);
    }

    private void AddReminderDetail(View panel)
    {
        var item = _service!.Snapshot.Reminders.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label("DETAIL", "#777181", 22.4f, 42, 28, 260, 36));
        if (item is null)
        {
            panel.Add(Label("Select a reminder", "#40394E", 44f, 52, 235, 668, 75, HorizontalAlignment.Center));
            panel.Add(Label("The reminder's due time, note, and actions will appear here.", "#777181", 25.6f, 80, 330, 612, 100, HorizontalAlignment.Center));
            return;
        }
        var detail = Surface(38, 86, 696, 505, "#FFFFFF", 22);
        detail.Name = $"ReminderDetailEntity-{item.Id}";
        detail.Add(Label(item.Completed ? "✓  COMPLETED" : item.DueAt < DateTimeOffset.Now ? "!  OVERDUE" : "○  ACTIVE", item.Completed ? "#55705B" : "#6B42B8", 22.4f, 30, 28, 620, 38));
        detail.Add(Label(item.Title, "#272330", 52.8f, 30, 82, 630, 105));
        detail.Add(Label("DUE", "#777181", 20f, 30, 205, 120, 30));
        detail.Add(Label(item.DueAt?.ToLocalTime().ToString("ddd, MMM d · HH:mm") ?? "No alert", "#40394E", 30.4f, 30, 240, 620, 48));
        detail.Add(Label("NOTE", "#777181", 20f, 30, 320, 120, 30));
        detail.Add(Label(string.IsNullOrWhiteSpace(item.Note) ? "No note" : item.Note, "#40394E", 27.2f, 30, 355, 620, 105));
        panel.Add(detail);
        if (!item.Completed)
        {
            var complete = Button("Complete", 40, 630, 204, 70, () => { _service.CompleteReminder(item.Id); });
            complete.Name = "ReminderDetailComplete";
            panel.Add(complete);
        }
        var edit = Button("Edit", 266, 630, 204, 70, () => { _editing = true; _newItem = false; Render(); });
        edit.Name = "ReminderDetailEdit";
        panel.Add(edit);
        var delete = Button("Delete", 492, 630, 204, 70, () => { _confirmDelete = true; Render(); });
        delete.Name = "ReminderDetailDelete";
        panel.Add(delete);
    }

    private void AddReminderEditor(View panel)
    {
        var original = _newItem ? null : _service!.Snapshot.Reminders.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label(_newItem ? "NEW REMINDER" : "EDIT REMINDER", "#777181", 22.4f, 42, 28, 360, 36));
        var title = Field(original?.Title ?? string.Empty, "Title", 42, 95, 688, 72);
        var due = Field(original?.DueAt?.ToString("O") ?? DateTimeOffset.Now.AddHours(1).ToString("O"), "RFC 3339 due time; leave blank for no alert", 42, 196, 688, 68);
        var note = new TextEditor
        {
            Text = original?.Note ?? string.Empty,
            PlaceholderText = "Note (optional)",
            PlaceholderTextColor = new Color(0.48f, 0.46f, 0.52f, 1),
            EnableEditing = true, Focusable = true, PixelSize = 28,
            Position = P(42, 300), Size = S(688, 220), BackgroundColor = new Color("#FFFFFF"),
        };
        title.Name = "ReminderEditorTitle";
        due.Name = "ReminderEditorDue";
        note.Name = "ReminderEditorNote";
        TrackInputFocus(note);
        var validation = Label(string.Empty, "#B3261E", 24f, 42, 550, 688, 54);
        panel.Add(title); panel.Add(due); panel.Add(note); panel.Add(validation);
        var cancel = Button("Cancel", 278, 655, 210, 72, () => { _editing = false; _newItem = false; Render(); });
        cancel.Name = "ReminderEditorCancel";
        panel.Add(cancel);
        var save = Button("Save", 514, 655, 216, 72, () =>
        {
            DateTimeOffset? dueAt = null;
            if (!string.IsNullOrWhiteSpace(due.Text) && !DateTimeOffset.TryParse(due.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            { validation.Text = "Use RFC 3339 date and time with an offset."; return; }
            else if (!string.IsNullOrWhiteSpace(due.Text)) dueAt = DateTimeOffset.Parse(due.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            try
            {
                var item = ReminderItem.Create(original?.Id ?? $"reminder-{Guid.NewGuid():N}", title.Text, dueAt, note.Text, original?.CreatedAt) with { Completed = original?.Completed ?? false, ActiveState = original?.ActiveState ?? "To-do" };
                var result = _newItem ? _service!.CreateReminder(item) : _service!.UpdateReminder(item);
                if (!result.Success) { validation.Text = result.Reason; return; }
                _selectedId = item.Id; _editing = false; _newItem = false; Render();
            }
            catch (ArgumentException exception) { validation.Text = exception.Message; }
        });
        save.Name = "ReminderEditorSave";
        panel.Add(save);
    }

    private void AddDeleteConfirmation(View canvas)
    {
        // Block underlying pointer controls and limit published context to the dialog.
        for (uint i = 0; i < canvas.ChildCount; i++)
        {
            var child = canvas.GetChildAt(i);
            child.IsEnabled = false;
            child.Sensitive = false;
            child.FocusableChildren = false;
        }
        var dialog = Surface(540, 330, 840, 420, "#FFFFFF", 28);
        dialog.Name = "ReminderDeleteDialog";
        dialog.BorderlineWidth = 2;
        dialog.BorderlineColor = new Color("#6B42B8");
        dialog.Add(Label("Delete reminder?", "#272330", 44, 48, 32, 744, 76));
        dialog.Add(Label("This reminder will be removed.", "#746F7E", 28, 48, 126, 744, 80));
        var cancel = Button("Cancel", 328, 292, 204, 72, () => { _confirmDelete = false; Render(); FocusByName("ReminderDetailDelete"); });
        cancel.Name = "ReminderDeleteCancel";
        var confirm = Button("Delete", 556, 292, 204, 72, () =>
        {
            if (_selectedId is null) return;
            var result = _service!.DeleteReminder(_selectedId);
            if (!result.Success) return;
            _confirmDelete = false; _selectedId = null; Render();
        });
        confirm.Name = "ReminderDeleteConfirm";
        dialog.Add(cancel); dialog.Add(confirm); canvas.Add(dialog);
    }

    private void AddReservationDetail(View panel)
    {
        var item = _service!.Snapshot.Reservations.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label("RESERVATION · COMMON SIMULATOR", "#6B42B8", 22.4f, 42, 28, 650, 38));
        if (item is null)
        {
            panel.Add(Label("Select a reservation", "#40394E", 44f, 52, 235, 668, 75, HorizontalAlignment.Center));
            panel.Add(Label("No tuner or recording backend is changed on Common Emulator.", "#777181", 25.6f, 80, 330, 612, 100, HorizontalAlignment.Center));
            return;
        }
        var program = Label(item.Program, "#272330", 52.8f, 44, 105, 680, 100);
        program.Name = $"ReminderDetailEntity-{item.Id}";
        panel.Add(program);
        panel.Add(Label($"{item.Kind} · {item.Channel}", "#6B42B8", 28.8f, 44, 224, 680, 52));
        panel.Add(Label($"{item.StartAt.ToLocalTime():ddd, MMM d · HH:mm}\n{item.EndAt.ToLocalTime():ddd, MMM d · HH:mm}\nRepeat: {item.Repeat}", "#40394E", 30.4f, 44, 310, 680, 180));
        var cancel = Button("Cancel reservation", 440, 620, 290, 72, () => { _service.CancelReservation(item.Id, item.Kind); _selectedId = null; });
        cancel.Name = "ReminderDetailCancelReservation";
        panel.Add(cancel);
    }

    private void OpenNew()
    {
        if (_section == "Reservations")
        {
            var start = DateTimeOffset.Now.AddHours(2);
            var index = _service!.Snapshot.Reservations.Count + 1;
            var kind = index % 2 == 0 ? ReservationKind.Recording : ReservationKind.Viewing;
            var item = ReservationItem.Create($"reservation-{Guid.NewGuid():N}", kind, $"Channel {index}", $"Demo program {index}", start, start.AddHours(1), ReservationRepeat.Once);
            var result = _service.AddReservation(item, kind);
            if (result.Success) _selectedId = item.Id;
            Render();
            return;
        }
        _editing = true; _newItem = true; _selectedId = null; Render();
    }

    private void SelectSection(string section)
    {
        _section = section; _selectedId = null; _editing = false; _newItem = false; _timeFilter = "All"; Render();
    }

    private void SelectItem(string id) { _selectedId = id; _editing = false; Render(); }

    private IReadOnlyList<ListItem> GetCurrentItems()
    {
        if (_section == "Reservations") return _service!.GetReservations()
            .Where(x => string.IsNullOrWhiteSpace(_search.AppliedKeyword) || x.Program.Contains(_search.AppliedKeyword, StringComparison.OrdinalIgnoreCase) || x.Channel.Contains(_search.AppliedKeyword, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ListItem(x.Id, $"{(x.Kind == ReservationKind.Recording ? "● REC" : "▷ VIEW")}  {x.Program}", $"{x.Channel} · {x.StartAt.ToLocalTime():MMM d HH:mm} · Simulated")).ToArray();
        var category = _section switch { "Today" => ReminderCategory.Today, "Upcoming" => ReminderCategory.Upcoming, "Overdue" => ReminderCategory.Overdue, "Completed" => ReminderCategory.Completed, _ => ReminderCategory.All };
        return _service!.SearchReminders(new ReminderQuery(_search.AppliedKeyword, category, 50))
            .Where(MatchesTimeFilter)
            .Select(x => new ListItem(x.Id, $"{(x.Completed ? "✓" : "○")}  {x.Title}", x.DueAt is null ? "No alert" : $"{x.DueAt.Value.ToLocalTime():MMM d · HH:mm}{(x.DueAt < DateTimeOffset.Now && !x.Completed ? " · Overdue" : string.Empty)}")).ToArray();
    }

    private bool MatchesTimeFilter(ReminderItem item)
    {
        if (_timeFilter == "All") return true;
        if (_timeFilter == "No alert") return item.DueAt is null;
        if (item.DueAt is null) return false;
        var hour = item.DueAt.Value.ToLocalTime().Hour;
        return _timeFilter switch
        {
            "Morning" => hour < 12,
            "Afternoon" => hour is >= 12 and < 18,
            "Evening" => hour >= 18,
            _ => true,
        };
    }

    private void QueueAnnotationRefresh()
    {
        _annotationTimer?.Stop();
        if (_paused || _refreshing || _activeRoot is null) return;
        // Update live focus/text immediately; the next layout replaces the same
        // snapshot atomically. Render/OnPause clear removed or hidden trees.
        PublishAnnotations();
        _annotationTimer?.Start();
    }

    private void PublishAnnotations()
    {
        if (_paused || _refreshing || _activeRoot is null || _service is null) return;
        var context = ReminderAnnotationPage.Create(_section, _editing, _newItem, _selectedId, _search.AppliedKeyword, _timeFilter, _confirmDelete);
        var pageId = context.Id;
        var page = context.State;
        var snapshots = new List<CurrentViewSnapshot>();
        var annotated = new HashSet<View>();
        var state = _service.Snapshot;
        var focused = NuiViewAnnotations.Focused(_activeRoot);
        void Capture(View? view, CurrentViewSnapshot snapshot)
        {
            if (view is null) return;
            var measured = NuiViewAnnotations.Measure(view, _activeRoot, focused, snapshot);
            if (measured is null) return;
            snapshots.Add(measured);
            annotated.Add(view);
        }
        void CaptureEntity(View? view, string id, string surface, bool includeNote)
        {
            var reminder = state.Reminders.FirstOrDefault(x => x.Id == id);
            var reservation = state.Reservations.FirstOrDefault(x => x.Id == id);
            if (reminder is not null)
                Capture(view, ReminderViewSnapshots.Reminder($"{pageId}:{surface}:{id}", reminder, includeNote));
            else if (reservation is not null)
                Capture(view, ReminderViewSnapshots.Reservation($"{pageId}:reservation-{surface}:{id}", reservation));
        }
        Capture(_activeRoot, ReminderViewSnapshots.Context(pageId, "Reminder.Page", context.Title, new { schemaVersion = 1, page }));
        if (_selectedId is { } detailId && !_editing)
            CaptureEntity(_activeRoot.FindChildByName($"ReminderDetailEntity-{detailId}"), detailId, "detail", includeNote: true);
        foreach (var id in _renderedItemIds)
            CaptureEntity(_activeRoot.FindChildByName($"ReminderEntity-{id}"), id, "item", includeNote: false);
        foreach (var (view, key) in NuiViewAnnotations.Controls(_activeRoot))
        {
            if (annotated.Contains(view)) continue;
            Capture(view, ReminderViewSnapshots.Context($"{pageId}:control:{key}", "Reminder.Control",
                NuiViewAnnotations.Description(view, key), new { schemaVersion = 1, page, draft = NuiViewAnnotations.ControlState(view, key) }));
        }
        ReminderViewActionProviderHost.Publish(snapshots);
    }

    private static View Surface(float x, float y, float w, float h, string color, float radius) => new()
    { Position = P(x, y), Size = S(w, h), BackgroundColor = new Color(color), CornerRadius = radius, FocusableChildren = true };

    private NuiButton Button(string text, float x, float y, float w, float h, Action action)
    {
        var button = new NuiButton { Text = text, Position = P(x, y), Size = S(w, h), Focusable = true, BackgroundColor = Color.White, TextColor = new Color("#292531"), CornerRadius = 12 };
        button.TextLabel.PixelSize = 28;
        button.FocusGained += (_, _) => { button.BorderlineWidth = 4; button.BorderlineColor = new Color("#6B42B8"); button.Scale = new Vector3(1.02f, 1.02f, 1); };
        button.FocusLost += (_, _) => { button.BorderlineWidth = 0; button.Scale = Vector3.One; };
        button.Clicked += (_, _) =>
        {
            ClearEditorFocus();
            action();
            if (!_paused) _editorFocus = CurrentEditorInput();
        };
        return button;
    }

    private TextField Field(string text, string placeholder, float x, float y, float w, float h)
    {
        var field = new TextField { Text = text, PlaceholderText = placeholder, EnableEditing = true, Focusable = true, PixelSize = 28, Position = P(x, y), Size = S(w, h), BackgroundColor = new Color("#FFFFFF") };
        TrackInputFocus(field);
        return field;
    }

    private static TextLabel Label(string text, string color, float pixelSize, float x, float y, float w, float h, HorizontalAlignment alignment = HorizontalAlignment.Begin) => new()
    { Text = text, TextColor = new Color(color), PixelSize = pixelSize, Position = P(x, y), Size = S(w, h), HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center, MultiLine = true };

    private static Position P(float x, float y) => new(x, y);
    private static Size S(float w, float h) => new(w, h);
    private sealed record ListItem(string Id, string Primary, string Secondary);

    private static void Main(string[] args) => new ReminderApplication().Run(args);
}
