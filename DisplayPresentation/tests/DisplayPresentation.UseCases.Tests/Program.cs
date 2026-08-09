using System.Text.Json;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;

var parser = new A2UiPresentationParser();
var valid = new PresentationInput(
    JsonSerializer.Serialize(new
    {
        surfaceUpdate = new
        {
            surfaceId = "fixture",
            components = new object[]
            {
                new { id = "root", component = new { Column = new { children = new[] { "headline", "group" } } } },
                new { id = "headline", component = new { Text = new { text = new { path = "/title" }, role = "headline" } } },
                new { id = "group", component = new { Column = new { children = new[] { "body" } } } },
                new { id = "body", component = new { Text = new { text = new { path = "/body" }, role = "body" } } },
            },
        },
    }),
    JsonSerializer.Serialize(new
    {
        dataModelUpdate = new { surfaceId = "fixture", path = "/", value = new { title = "Fixture title", body = "Fixture body" } },
    }));

var outcome = parser.Parse(valid);
Assert(outcome.IsSuccess, "Valid bounded A2UI must produce a semantic tree.");
var root = outcome.Plan!.Surface.Root as VerticalGroup;
Assert(root?.Id == "root" && root.Children.Count == 2 && root.Children[0] is TextValue { Role: "headline", Value: "Fixture title" } &&
    root.Children[1] is VerticalGroup { Children.Count: 1 } group && group.Children[0] is TextValue { Value: "Fixture body" },
    "The semantic tree must preserve supported hierarchy, roles, and document order.");

Assert(parser.Parse(new PresentationInput("{", valid.Document)).Failure?.Kind == RenderFailureKind.InvalidInput, "Malformed JSON must be typed invalid input.");
Assert(parser.Parse(new PresentationInput(new string(' ', 1), valid.Document)).Failure?.Kind == RenderFailureKind.InvalidInput, "Empty template must be typed invalid input.");
Assert(parser.Parse(new PresentationInput(valid.Template, valid.Document.Replace("fixture", "other"))).Failure?.Kind == RenderFailureKind.InvalidInput, "Mismatched surface IDs must be rejected.");
Assert(parser.Parse(valid with { Template = valid.Template.Replace("\"Text\"", "\"Image\"") }).Failure?.Kind == RenderFailureKind.Unsupported, "Unsupported components must not receive an invented layout.");
Assert(parser.Parse(valid with { Template = valid.Template.Replace("\"role\":\"headline\"", "\"color\":\"red\"") }).Failure?.Kind == RenderFailureKind.Unsupported, "Payload styling must be rejected.");
Assert(parser.Parse(valid with { Template = valid.Template.Replace("\"id\":\"group\"", "\"id\":\"missing\"") }).Failure?.Kind == RenderFailureKind.InvalidInput, "Unknown child references must be rejected.");
Assert(parser.Parse(valid, new CancellationToken(true)).Failure?.Kind == RenderFailureKind.Cancelled, "Cancelled work must be typed and not render.");
Assert(parser.Parse(valid with { Document = new string('x', A2UiPresentationParser.MaximumJsonCharacters + 1) }).Failure?.Kind == RenderFailureKind.InvalidInput, "Oversized payloads must be rejected.");
var overlong = valid with { Document = valid.Document.Replace("Fixture title", new string('x', A2UiPresentationParser.MaximumDisplayCharacters + 1)) };
var bounded = parser.Parse(overlong).Plan!.Surface;
var roundTrip = A2UiPresentationSerializer.Serialize(bounded);
Assert(parser.Parse(roundTrip) is { IsSuccess: true, Plan: { Surface.Root: VerticalGroup { Children: [TextValue { Value.Length: A2UiPresentationParser.MaximumDisplayCharacters }, _] } } },
    "View round-trip data must be reconstructed from bounded semantic values, not retained raw payload JSON.");

var coordinator = new PresentationRenderCoordinator(parser);
var notifications = 0;
coordinator.Rendered += (_, _) => notifications++;
Assert(coordinator.Submit(valid).IsSuccess && coordinator.Current.IsSuccess, "The shared coordinator must publish the accepted immutable render tree.");
Assert(!coordinator.Submit(valid with { Template = "{" }).IsSuccess && !coordinator.Current.IsSuccess && notifications == 2,
    "A newer invalid request must replace the prior visible result rather than retain stale content.");
Assert(!coordinator.Dismiss().IsSuccess && !coordinator.Current.IsSuccess && notifications == 3,
    "Dismissing the profile recovery state must not resurrect a stale accepted Presentation.");
Console.WriteLine("DisplayPresentation.UseCases.Tests: PASS");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
