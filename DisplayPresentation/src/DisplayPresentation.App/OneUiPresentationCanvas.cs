using DisplayPresentation.Domain;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NuiButton = Tizen.NUI.Components.Button;

namespace DisplayPresentation.App;

/// <summary>
/// Profile-owned NUI composition for the parser's immutable semantic tree. It deliberately has
/// no JSON, payload styles, or action callbacks: those remain outside this NUI mapping seam.
/// </summary>
internal sealed class OneUiPresentationCanvas
{
    internal const float DesignWidth = 1920f;
    internal const float DesignHeight = 1080f;
    private const float Gutter = 132f;
    private const float ContentWidth = 1656f;
    private const float Top = 176f;

    internal OneUiPresentationCanvas(RenderOutcome outcome, Action dismiss)
    {
        Canvas = new View
        {
            Name = "DisplayPresentationReferenceCanvas",
            Size = new Size(DesignWidth, DesignHeight),
            ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color("#F7F7F8FF"),
            FocusableChildren = true,
        };

        Canvas.Add(Label("Presentation", "#6F7078FF", 5f, new Position(Gutter, 56f), new Size(ContentWidth, 36f), HorizontalAlignment.Begin));
        if (outcome.Plan is { } plan)
        {
            AddSurface(plan.Surface);
        }
        else
        {
            AddFailure(outcome.Failure?.Message ?? "No presentation is currently available.", dismiss);
        }
    }

    internal View Canvas { get; }

    private void AddSurface(SemanticSurface surface)
    {
        var section = new View
        {
            Name = $"OneUiSection:{surface.SurfaceId}",
            Position = new Position(Gutter, Top),
            Size = new Size(ContentWidth, 620f),
            BackgroundColor = Color.White,
            CornerRadius = 18f,
            BorderlineWidth = 1f,
            BorderlineColor = new Color("#D8D8DEFF"),
        };
        Canvas.Add(section);
        var cursor = 52f;
        AddNode(section, surface.Root, 52f, ref cursor);
    }

    private static void AddNode(View parent, SemanticNode node, float left, ref float cursor)
    {
        switch (node)
        {
            case TextValue text:
                var height = RoleHeight(text.Role);
                parent.Add(Label(text.Value, RoleColor(text.Role), RolePointSize(text.Role), new Position(left, cursor), new Size(ContentWidth - (left * 2f), height), HorizontalAlignment.Begin));
                cursor += height + 18f;
                return;
            case VerticalGroup group:
                foreach (var child in group.Children)
                {
                    AddNode(parent, child, left, ref cursor);
                }
                cursor += 10f;
                return;
            default:
                throw new InvalidOperationException("Only profile-validated semantic nodes may reach NUI composition.");
        }
    }

    private void AddFailure(string reason, Action dismiss)
    {
        var section = new View
        {
            Name = "OneUiProfileError",
            Position = new Position(Gutter, Top),
            Size = new Size(ContentWidth, 454f),
            BackgroundColor = Color.White,
            CornerRadius = 18f,
            BorderlineWidth = 1f,
            BorderlineColor = new Color("#E9BBB6FF"),
        };
        section.Add(Label("PRESENTATION UNAVAILABLE", "#B3261EFF", 4.5f, new Position(52f, 52f), new Size(1250f, 42f), HorizontalAlignment.Begin));
        section.Add(Label("This presentation cannot be shown", "#1B1B20FF", 12f, new Position(52f, 102f), new Size(1450f, 76f), HorizontalAlignment.Begin));
        section.Add(Label(reason, "#6F7078FF", 6f, new Position(52f, 198f), new Size(1450f, 104f), HorizontalAlignment.Begin));
        var dismissButton = new NuiButton
        {
            Name = "OneUiRecoveryDismiss",
            Text = "Dismiss",
            Position = new Position(52f, 330f),
            Size = new Size(210f, 62f),
            Focusable = true,
        };
        dismissButton.Clicked += (_, _) => dismiss();
        section.Add(dismissButton);
        RecoveryFocus = dismissButton;
        Canvas.Add(section);
    }

    internal View? RecoveryFocus { get; private set; }

    private static float RoleHeight(string role) => role switch { "headline" => 86f, "title" => 62f, "label" => 40f, _ => 54f };
    private static float RolePointSize(string role) => role switch { "headline" => 13f, "title" => 9f, "label" => 5f, "supporting" => 6f, _ => 7f };
    private static string RoleColor(string role) => role is "label" or "supporting" ? "#6F7078FF" : "#1B1B20FF";

    private static TextLabel Label(string text, string color, float pointSize, Position position, Size size, HorizontalAlignment alignment) => new(text)
    {
        Position = position,
        Size = size,
        TextColor = new Color(color),
        PointSize = pointSize,
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
        Ellipsis = true,
        MultiLine = true,
    };
}
