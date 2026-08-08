using System.Globalization;
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
    private string _keyword = string.Empty;
    private string _timeFilter = "All";
    private SynchronizationContext? _uiContext;
    private IReadOnlyList<ReminderViewSnapshot> _published = [];
    private ProportionalViewport _viewport;

    protected override void OnCreate()
    {
        base.OnCreate();
        _uiContext = SynchronizationContext.Current;
        var dataPath = Tizen.Applications.Application.Current.DirectoryInfo.Data;
        _service = new ScheduleService(
            new JsonScheduleStore(System.IO.Path.Combine(dataPath, "reminder-data.json")),
            new DeterministicReservationSimulator());
        _service.Changed += OnServiceChanged;
        ReminderScheduleActionProviderHost.Start(_service);
        ReminderViewActionProviderHost.Start();
        Window.Default.KeyEvent += OnKeyEvent;
        Window.Default.Resized += OnWindowResized;
        Window.Default.InsetsChanged += OnWindowResized;
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        Render();
    }

    protected override void OnPause()
    {
        _published = [];
        ReminderViewActionProviderHost.Clear();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Render();
    }

    protected override void OnTerminate()
    {
        if (_service is not null) _service.Changed -= OnServiceChanged;
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.Resized -= OnWindowResized;
        Window.Default.KeyEvent -= OnKeyEvent;
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        ReminderViewActionProviderHost.Clear();
        base.OnTerminate();
    }

    private void OnWindowResized(object? sender, EventArgs args)
    {
        Render();
    }

    private void OnServiceChanged()
    {
        if (_uiContext is null) return;
        _uiContext.Post(_ => Render(), null);
    }

    private void OnKeyEvent(object? sender, Window.KeyEventArgs args)
    {
        if (args.Key.State != Key.StateType.Down) return;
        var key = args.Key.KeyPressedName;
        if (key is "XF86Back" or "Escape")
        {
            if (_editing)
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

    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs args) => PublishAnnotations();

    private void Render()
    {
        if (_service is null) return;

        var size = Window.Default.WindowSize;
        var insets = Window.Default.GetInsets();
        if (!ProportionalViewport.TryCreate(
                size.Width,
                size.Height,
                insets.Start,
                insets.Top,
                insets.End,
                insets.Bottom,
                out var viewport))
        {
            return;
        }

        if (_root is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_root);
            _root.Dispose();
        }

        _viewport = viewport;
        var scale = _viewport.Scale;
        _root = new View
        {
            Name = "ReminderWorkspace",
            AccessibilityName = "Reminder focused workspace",
            Size = new Size(size.Width, size.Height),
            BackgroundColor = new Color("#F7F6FB"),
            FocusableChildren = true,
        };
        AddHeader(_root, scale, size.Width);
        AddNavigation(_root, scale);
        AddList(_root, scale);
        AddDetail(_root, scale);
        Window.Default.GetDefaultLayer().Add(_root);
        _activeRoot = _root;

        var preferred = _selectedId is not null ? _root.FindChildByName($"ReminderEntity-{_selectedId}") : null;
        preferred ??= _root.FindChildByName($"ReminderNav-{_section}");
        if (preferred is not null) FocusManager.Instance.SetCurrentFocusView(preferred);
        PublishAnnotations();
    }

    private void AddHeader(View root, float scale, float width)
    {
        root.Add(CanvasLabel("Reminder", "#201D29", 9.5f * scale, 58, 28, 550, 84, scale));
        root.Add(CanvasLabel("Focused workspace · Common Emulator simulator", "#746F7E", 3.3f * scale, 62, 104, 720, 42, scale));
        var add = CanvasButton(_section == "Reservations" ? "+ Add simulated reservation" : "+ Add reminder", 1550, 50, 310, 68, scale, OpenNew);
        add.Name = "ReminderAdd";
        add.AccessibilityName = _section == "Reservations" ? "Add simulated reservation" : "Add reminder";
        root.Add(add);
    }

    private void AddNavigation(View root, float scale)
    {
        var panel = CanvasSurface(50, 164, 300, 850, scale, "#ECE9F3", 28);
        panel.Name = "ReminderNavigation";
        panel.Add(Label("SMART LISTS", "#777181", 2.8f * scale, 28, 24, 240, 40, scale));
        for (var index = 0; index < Navigation.Length; index++)
        {
            var name = Navigation[index];
            var selected = name == _section;
            var button = Button((selected ? "●  " : "○  ") + name, 22, 82 + index * 100, 256, 74, scale, () => SelectSection(name));
            button.Name = $"ReminderNav-{name}";
            button.AccessibilityName = $"{name} smart list{(selected ? ", selected" : string.Empty)}";
            button.BackgroundColor = new Color(selected ? "#DED5F5" : "#F7F6FB");
            button.TextColor = new Color("#292531");
            panel.Add(button);
        }
        panel.Add(Label("Reservations use deterministic\napp-owned simulator jobs.", "#746F7E", 2.6f * scale, 28, 720, 240, 74, scale));
        root.Add(panel);
    }

    private void AddList(View root, float scale)
    {
        var panel = CanvasSurface(374, 164, 700, 850, scale, "#FFFFFF", 28);
        panel.Name = "ReminderListPane";
        var items = GetCurrentItems();
        panel.Add(Label(_section, "#272330", 6.2f * scale, 34, 25, 430, 65, scale));
        panel.Add(Label($"{items.Count} items", "#777181", 3.0f * scale, 565, 39, 100, 40, scale));
        var search = Field(_keyword, "Search title or note", 32, 88, 460, 62, scale);
        search.Name = "ReminderSearch";
        search.AccessibilityName = "Search reminders";
        search.TextChanged += (_, args) => _keyword = args.TextField.Text;
        panel.Add(search);
        var applySearch = Button("Search", 510, 88, 158, 62, scale, Render);
        applySearch.Name = "ReminderSearchApply";
        applySearch.AccessibilityName = "Apply reminder search";
        panel.Add(applySearch);
        var listTop = 174;
        if (_section != "Reservations")
        {
            for (var filterIndex = 0; filterIndex < TimeFilters.Length; filterIndex++)
            {
                var filter = TimeFilters[filterIndex];
                var chip = Button(filter, 32 + filterIndex * 127, 164, 118, 48, scale, () => { _timeFilter = filter; Render(); });
                chip.Name = $"ReminderFilter-{filter.Replace(" ", string.Empty)}";
                chip.AccessibilityName = $"{filter} reminder filter{(filter == _timeFilter ? ", selected" : string.Empty)}";
                chip.BackgroundColor = new Color(filter == _timeFilter ? "#2D2933" : "#F0EDF5");
                chip.TextColor = new Color(filter == _timeFilter ? "#FFFFFF" : "#40394E");
                panel.Add(chip);
            }
            listTop = 228;
        }
        root.Add(panel);

        if (items.Count == 0)
        {
            panel.Add(Label("Nothing here yet", "#40394E", 5.0f * scale, 80, 280, 540, 70, scale, HorizontalAlignment.Center));
            panel.Add(Label(_section == "Reservations" ? "Add a deterministic viewing or recording simulation." : "Choose Add reminder to create your first item.", "#777181", 3.2f * scale, 80, 360, 540, 100, scale, HorizontalAlignment.Center));
            return;
        }

        for (var index = 0; index < Math.Min(items.Count, 6); index++)
        {
            var item = items[index];
            var isSelected = item.Id == _selectedId;
            var button = Button(item.Primary + "  ·  " + item.Secondary, 32, listTop + index * 96, 636, 80, scale, () => SelectItem(item.Id));
            button.Name = $"ReminderEntity-{item.Id}";
            button.AccessibilityName = item.Primary + ", " + item.Secondary;
            button.BackgroundColor = new Color(isSelected ? "#E6DDF8" : "#F7F6FB");
            button.TextColor = new Color("#292531");
            panel.Add(button);
        }
    }

    private void AddDetail(View root, float scale)
    {
        var panel = CanvasSurface(1098, 164, 772, 850, scale, "#F0EDF5", 28);
        panel.Name = "ReminderDetailPane";
        if (_editing && _section != "Reservations") AddReminderEditor(panel, scale);
        else if (_section == "Reservations") AddReservationDetail(panel, scale);
        else AddReminderDetail(panel, scale);
        root.Add(panel);
    }

    private void AddReminderDetail(View panel, float scale)
    {
        var item = _service!.Snapshot.Reminders.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label("DETAIL", "#777181", 2.8f * scale, 42, 28, 260, 36, scale));
        if (item is null)
        {
            panel.Add(Label("Select a reminder", "#40394E", 5.5f * scale, 52, 235, 668, 75, scale, HorizontalAlignment.Center));
            panel.Add(Label("The reminder's due time, note, and actions will appear here.", "#777181", 3.2f * scale, 80, 330, 612, 100, scale, HorizontalAlignment.Center));
            return;
        }
        var detail = Surface(38, 86, 696, 505, scale, "#FFFFFF", 22);
        detail.Name = $"ReminderDetailEntity-{item.Id}";
        detail.Add(Label(item.Completed ? "✓  COMPLETED" : item.DueAt < DateTimeOffset.Now ? "!  OVERDUE" : "○  ACTIVE", item.Completed ? "#55705B" : "#6B42B8", 2.8f * scale, 30, 28, 620, 38, scale));
        detail.Add(Label(item.Title, "#272330", 6.6f * scale, 30, 82, 630, 105, scale));
        detail.Add(Label("DUE", "#777181", 2.5f * scale, 30, 205, 120, 30, scale));
        detail.Add(Label(item.DueAt?.ToLocalTime().ToString("ddd, MMM d · HH:mm") ?? "No alert", "#40394E", 3.8f * scale, 30, 240, 620, 48, scale));
        detail.Add(Label("NOTE", "#777181", 2.5f * scale, 30, 320, 120, 30, scale));
        detail.Add(Label(string.IsNullOrWhiteSpace(item.Note) ? "No note" : item.Note, "#40394E", 3.4f * scale, 30, 355, 620, 105, scale));
        panel.Add(detail);
        if (!item.Completed)
        {
            var complete = Button("Complete", 40, 630, 204, 70, scale, () => { _service.CompleteReminder(item.Id); });
            complete.Name = "ReminderDetailComplete";
            panel.Add(complete);
        }
        var edit = Button("Edit", 266, 630, 204, 70, scale, () => { _editing = true; _newItem = false; Render(); });
        edit.Name = "ReminderDetailEdit";
        panel.Add(edit);
        var delete = Button("Delete", 492, 630, 204, 70, scale, () => { _service.DeleteReminder(item.Id); _selectedId = null; });
        delete.Name = "ReminderDetailDelete";
        panel.Add(delete);
    }

    private void AddReminderEditor(View panel, float scale)
    {
        var original = _newItem ? null : _service!.Snapshot.Reminders.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label(_newItem ? "NEW REMINDER" : "EDIT REMINDER", "#777181", 2.8f * scale, 42, 28, 360, 36, scale));
        var title = Field(original?.Title ?? string.Empty, "Title", 42, 95, 688, 72, scale);
        var due = Field(original?.DueAt?.ToString("O") ?? DateTimeOffset.Now.AddHours(1).ToString("O"), "RFC 3339 due time; leave blank for no alert", 42, 196, 688, 68, scale);
        var note = new TextEditor
        {
            Text = original?.Note ?? string.Empty,
            PlaceholderText = "Note (optional)",
            PlaceholderTextColor = new Color(0.48f, 0.46f, 0.52f, 1),
            EnableEditing = true, Focusable = true,
            Position = P(42, 300, scale), Size = S(688, 220, scale), BackgroundColor = new Color("#FFFFFF"),
        };
        var validation = Label(string.Empty, "#B3261E", 3.0f * scale, 42, 550, 688, 54, scale);
        panel.Add(title); panel.Add(due); panel.Add(note); panel.Add(validation);
        panel.Add(Button("Cancel", 278, 655, 210, 72, scale, () => { _editing = false; _newItem = false; Render(); }));
        panel.Add(Button("Save", 514, 655, 216, 72, scale, () =>
        {
            DateTimeOffset? dueAt = null;
            if (!string.IsNullOrWhiteSpace(due.Text) && !DateTimeOffset.TryParse(due.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            { validation.Text = "Use RFC 3339 date and time with an offset."; return; }
            else if (!string.IsNullOrWhiteSpace(due.Text)) dueAt = DateTimeOffset.Parse(due.Text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            try
            {
                var item = ReminderItem.Create(original?.Id ?? $"reminder-{Guid.NewGuid():N}", title.Text, dueAt, note.Text, original?.CreatedAt) with { Completed = original?.Completed ?? false };
                var result = _newItem ? _service!.CreateReminder(item) : _service!.UpdateReminder(item);
                if (!result.Success) { validation.Text = result.Reason; return; }
                _selectedId = item.Id; _editing = false; _newItem = false; Render();
            }
            catch (ArgumentException exception) { validation.Text = exception.Message; }
        }));
    }

    private void AddReservationDetail(View panel, float scale)
    {
        var item = _service!.Snapshot.Reservations.FirstOrDefault(x => x.Id == _selectedId);
        panel.Add(Label("RESERVATION · COMMON SIMULATOR", "#6B42B8", 2.8f * scale, 42, 28, 650, 38, scale));
        if (item is null)
        {
            panel.Add(Label("Select a reservation", "#40394E", 5.5f * scale, 52, 235, 668, 75, scale, HorizontalAlignment.Center));
            panel.Add(Label("No tuner or recording backend is changed on Common Emulator.", "#777181", 3.2f * scale, 80, 330, 612, 100, scale, HorizontalAlignment.Center));
            return;
        }
        panel.Add(Label(item.Program, "#272330", 6.6f * scale, 44, 105, 680, 100, scale));
        panel.Add(Label($"{item.Kind} · {item.Channel}", "#6B42B8", 3.6f * scale, 44, 224, 680, 52, scale));
        panel.Add(Label($"{item.StartAt.ToLocalTime():ddd, MMM d · HH:mm}\n{item.EndAt.ToLocalTime():ddd, MMM d · HH:mm}\nRepeat: {item.Repeat}", "#40394E", 3.8f * scale, 44, 310, 680, 180, scale));
        var cancel = Button("Cancel reservation", 440, 620, 290, 72, scale, () => { _service.CancelReservation(item.Id, item.Kind); _selectedId = null; });
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
            .Where(x => string.IsNullOrWhiteSpace(_keyword) || x.Program.Contains(_keyword, StringComparison.OrdinalIgnoreCase) || x.Channel.Contains(_keyword, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ListItem(x.Id, $"{(x.Kind == ReservationKind.Recording ? "● REC" : "▷ VIEW")}  {x.Program}", $"{x.Channel} · {x.StartAt.ToLocalTime():MMM d HH:mm} · Simulated")).ToArray();
        var category = _section switch { "Today" => ReminderCategory.Today, "Upcoming" => ReminderCategory.Upcoming, "Overdue" => ReminderCategory.Overdue, "Completed" => ReminderCategory.Completed, _ => ReminderCategory.All };
        return _service!.SearchReminders(new ReminderQuery(_keyword, category, 50))
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

    private void PublishAnnotations()
    {
        if (_activeRoot is null || _service is null) return;
        var snapshots = new List<ReminderViewSnapshot>();
        var state = _service.Snapshot;
        View? focusedView = null;
        try { focusedView = FocusManager.Instance.GetCurrentFocusView(); } catch { }
        var detailId = _selectedId;
        if (detailId is not null && !_editing)
        {
            var detailView = _activeRoot.FindChildByName($"ReminderDetailEntity-{detailId}");
            AddSnapshot(detailView, state.Reminders.FirstOrDefault(x => x.Id == detailId), state.Reservations.FirstOrDefault(x => x.Id == detailId), snapshots, focusedView, includeNote: true);
        }
        foreach (var item in GetCurrentItems())
        {
            var view = _activeRoot.FindChildByName($"ReminderEntity-{item.Id}");
            AddSnapshot(view, state.Reminders.FirstOrDefault(x => x.Id == item.Id), state.Reservations.FirstOrDefault(x => x.Id == item.Id), snapshots, focusedView, includeNote: false);
        }
        _published = snapshots;
        ReminderViewActionProviderHost.Publish(_published);
    }

    private static void AddSnapshot(View? view, ReminderItem? reminder, ReservationItem? reservation, List<ReminderViewSnapshot> target, View? focusedView, bool includeNote)
    {
        if (view is null || (reminder is null && reservation is null)) return;
        try
        {
            var bounds = view.CalculateScreenPositionSize();
            var width = bounds.Z > 0 ? bounds.Z : view.Size.Width;
            var height = bounds.W > 0 ? bounds.W : view.Size.Height;
            if (width <= 0 || height <= 0) return;
            double? windowX = null, windowY = null;
            try { using var position = Window.Default.WindowPosition; windowX = bounds.X - position.X; windowY = bounds.Y - position.Y; } catch { }
            var surface = view.Name.StartsWith("ReminderDetailEntity-", StringComparison.Ordinal) ? "detail" : "item";
            var entityId = reminder?.Id ?? reservation!.Id;
            var viewId = reminder is not null ? $"reminder:{surface}:{entityId}" : $"reminder:reservation-{surface}:{entityId}";
            target.Add(new ReminderViewSnapshot(reminder, reservation, bounds.X, bounds.Y, windowX, windowY, width, height,
                viewId, ReferenceEquals(view, focusedView), includeNote));
        }
        catch { }
    }

    private View CanvasSurface(float x, float y, float w, float h, float scale, string color, float radius)
    {
        var surface = Surface(x, y, w, h, scale, color, radius);
        surface.Position = CanvasPosition(x, y, scale);
        return surface;
    }

    private NuiButton CanvasButton(string text, float x, float y, float w, float h, float scale, Action action)
    {
        var button = Button(text, x, y, w, h, scale, action);
        button.Position = CanvasPosition(x, y, scale);
        return button;
    }

    private TextLabel CanvasLabel(string text, string color, float pointSize, float x, float y, float w, float h, float scale)
    {
        var label = Label(text, color, pointSize, x, y, w, h, scale);
        label.Position = CanvasPosition(x, y, scale);
        return label;
    }

    private Position CanvasPosition(float x, float y, float scale) =>
        new(_viewport.OffsetX + (x * scale), _viewport.OffsetY + (y * scale));

    private static View Surface(float x, float y, float w, float h, float scale, string color, float radius) => new()
    { Position = P(x, y, scale), Size = S(w, h, scale), BackgroundColor = new Color(color), CornerRadius = radius * scale, FocusableChildren = true };

    private static NuiButton Button(string text, float x, float y, float w, float h, float scale, Action action)
    {
        var button = new NuiButton { Text = text, Position = P(x, y, scale), Size = S(w, h, scale), Focusable = true };
        button.Clicked += (_, _) => action();
        return button;
    }

    private static TextField Field(string text, string placeholder, float x, float y, float w, float h, float scale) => new()
    { Text = text, PlaceholderText = placeholder, EnableEditing = true, Focusable = true, Position = P(x, y, scale), Size = S(w, h, scale), BackgroundColor = new Color("#FFFFFF") };

    private static TextLabel Label(string text, string color, float pointSize, float x, float y, float w, float h, float scale, HorizontalAlignment alignment = HorizontalAlignment.Begin) => new()
    { Text = text, TextColor = new Color(color), PointSize = pointSize, Position = P(x, y, scale), Size = S(w, h, scale), HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center, MultiLine = true };

    private static Position P(float x, float y, float scale) => new(x * scale, y * scale);
    private static Size S(float w, float h, float scale) => new(w * scale, h * scale);
    private sealed record ListItem(string Id, string Primary, string Secondary);

    private static void Main(string[] args) => new ReminderApplication().Run(args);
}
