using System.Text.Json;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;

var parser = new A2UiPresentationParser();
var valid = new PresentationInput(
    JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "fixture", components = new object[] { new { id = "root", component = new { Column = new { } } }, new { id = "title", component = new { Text = new { text = new { path = "/title" } } } }, new { id = "body", component = new { Text = new { text = new { path = "/body" } } } } } } }),
    JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId = "fixture", path = "/", value = new { title = "Fixture title", body = "Fixture body" } } }));
var outcome = parser.Parse(valid);
Assert(outcome.IsSuccess && outcome.Plan!.Title == "Fixture title" && outcome.Plan.Body == "Fixture body", "Valid bounded A2UI must produce a deterministic render plan.");
Assert(parser.Parse(new PresentationInput("{", valid.Document)).Failure?.Kind == RenderFailureKind.InvalidInput, "Malformed JSON must be typed invalid input.");
Assert(parser.Parse(new PresentationInput(new string(' ', 1), valid.Document)).Failure?.Kind == RenderFailureKind.InvalidInput, "Empty template must be typed invalid input.");
Assert(parser.Parse(new PresentationInput(valid.Template, valid.Document.Replace("fixture", "other"))).Failure?.Kind == RenderFailureKind.InvalidInput, "Mismatched surface IDs must be rejected.");
var unsupported = valid with { Template = valid.Template.Replace("\"Text\"", "\"Image\"") };
Assert(parser.Parse(unsupported).Failure?.Kind == RenderFailureKind.Unsupported, "Unsupported components must not receive an invented layout.");
Assert(parser.Parse(valid, new CancellationToken(true)).Failure?.Kind == RenderFailureKind.Cancelled, "Cancelled work must be typed and not render.");
Assert(parser.Parse(valid with { Document = new string('x', A2UiPresentationParser.MaximumJsonCharacters + 1) }).Failure?.Kind == RenderFailureKind.InvalidInput, "Oversized payloads must be rejected.");
Console.WriteLine("DisplayPresentation.UseCases.Tests: PASS");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
