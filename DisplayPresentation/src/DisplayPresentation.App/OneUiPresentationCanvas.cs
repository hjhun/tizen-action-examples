using DisplayPresentation.Domain;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NuiButton = Tizen.NUI.Components.Button;

namespace DisplayPresentation.App;

/// <summary>Renderer-owned geometry for one bounded semantic page.</summary>
internal sealed class OneUiPresentationCanvas
{
    internal const float DesignWidth = 1920f;
    internal const float DesignHeight = 1080f;
    private const float ContentWidth = 1656f;
    private readonly List<NuiButton> _controls = [];

    internal OneUiPresentationCanvas(RenderOutcome outcome, int page, int pageCount,
        Action<int> changePage, Action dismiss, string? preferredControl)
    {
        Canvas = new View
        {
            Name = "DisplayPresentationReferenceCanvas", Size = new Size(DesignWidth, DesignHeight),
            ParentOrigin = ParentOrigin.TopLeft, PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color("#F7F7F8FF"), FocusableChildren = true,
        };
        Canvas.Add(Label("Presentation", "#6F7078FF", 24f, 132f, 56f, ContentWidth, 36f));
        Content = new View
        {
            Name = "PresentationContent", Position = new Position(132f, 176f), Size = new Size(ContentWidth, 660f),
            BackgroundColor = Color.White, CornerRadius = 18f, BorderlineWidth = 1f,
            BorderlineColor = new Color(outcome.Failure is null ? "#D8D8DEFF" : "#E9BBB6FF"),
        };
        Canvas.Add(Content);
        var cursor = 40f;
        if (outcome.Plan is { } plan)
        {
            AddNode(Content, plan.Surface.Root, ref cursor);
            if (cursor == 40f) Content.Add(Label("No items", "#1B1B20FF", 28f, 52f, 40f, 1552f, 120f));
        }
        else if (outcome.Failure is { } failure)
        {
            Content.Add(Label("This presentation cannot be shown", "#B3261EFF", 36f, 52f, 40f, 1552f, 120f));
            Content.Add(Label(failure.Message, "#1B1B20FF", 28f, 52f, 164f, 1552f, 120f));
        }
        else Content.Add(Label("No presentation is currently available.", "#1B1B20FF", 28f, 52f, 40f, 1552f, 120f));
        if (pageCount > 1)
        {
            AddButton("previous", "Previous", 132f, page > 0, () => changePage(-1));
            Canvas.Add(Label($"{page + 1} / {pageCount}", "#6F7078FF", 26f, 340f, 884f, 180f, 68f));
            AddButton("next", "Next", 544f, page < pageCount - 1, () => changePage(1));
        }
        AddButton("dismiss", "Dismiss", 1604f, true, dismiss);
        InitialFocus = _controls.FirstOrDefault(x => x.Name == preferredControl && x.IsEnabled)
            ?? _controls.FirstOrDefault(x => x.Name == "next" && x.IsEnabled)
            ?? _controls.First(x => x.IsEnabled);
    }

    internal View Canvas { get; }
    internal View Content { get; }
    internal NuiButton InitialFocus { get; }
    internal IReadOnlyList<NuiButton> Controls => _controls;

    internal void MoveFocus(int direction)
    {
        var enabled = _controls.Where(x => x.IsEnabled).ToArray();
        var index = Array.FindIndex(enabled, x => x == FocusManager.Instance.GetCurrentFocusView());
        FocusManager.Instance.SetCurrentFocusView(enabled[(index + direction + enabled.Length) % enabled.Length]);
    }

    private void AddButton(string name, string text, float x, bool enabled, Action activate)
    {
        var button = new NuiButton
        {
            Name = name, Text = text, AccessibilityName = text, Position = new Position(x, 884f),
            Size = new Size(184f, 68f), Focusable = enabled, IsEnabled = enabled,
            BackgroundColor = Color.White, CornerRadius = 12f,
            BorderlineWidth = 1f, BorderlineColor = new Color("#D8D8DEFF"),
        };
        button.TextLabel.PixelSize = 28f;
        button.TextColor = new Color(enabled ? "#1B1B20FF" : "#8B8B90FF");
        button.FocusGained += (_, _) =>
        {
            button.BorderlineWidth = 4f;
            button.BorderlineColor = new Color("#1466C3FF");
            button.BackgroundColor = new Color("#E8F1FBFF");
            button.Scale = new Vector3(1.02f, 1.02f, 1f);
        };
        button.FocusLost += (_, _) =>
        {
            button.BorderlineWidth = 1f;
            button.BorderlineColor = new Color("#D8D8DEFF");
            button.BackgroundColor = Color.White;
            button.Scale = Vector3.One;
        };
        button.Clicked += (_, _) => { if (button.IsEnabled) activate(); };
        _controls.Add(button);
        Canvas.Add(button);
    }

    private static void AddNode(View parent, SemanticNode node, ref float cursor)
    {
        if (node is TextValue text)
        {
            var label = Label(text.Value, text.Role is "label" or "supporting" ? "#6F7078FF" : "#1B1B20FF",
                text.Role switch { "headline" => 44f, "title" => 36f, "label" => 24f, "supporting" => 26f, _ => 28f },
                52f, cursor, 1552f, 120f);
            label.Name = "PresentationText-" + text.Id;
            parent.Add(label);
            cursor += 124f;
            return;
        }
        if (node is not VerticalGroup group) throw new InvalidOperationException("Only validated semantic nodes may reach NUI composition.");
        // Keep semantic group actors, in their parent's coordinate system.
        var stack = new View { Name = "PresentationGroup-" + group.Id, Size = new Size(ContentWidth, 660f) };
        parent.Add(stack);
        foreach (var child in group.Children) AddNode(stack, child, ref cursor);
    }

    private static TextLabel Label(string text, string color, float pixels, float x, float y, float width, float height) => new(text)
    {
        Position = new Position(x, y), Size = new Size(width, height), TextColor = new Color(color), PixelSize = pixels,
        HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center,
        Ellipsis = true, MultiLine = true,
    };
}
