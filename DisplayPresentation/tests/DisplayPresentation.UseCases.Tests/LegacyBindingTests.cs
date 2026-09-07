using System.Text.Json;
using System.Text.Json.Nodes;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;

internal static class LegacyBindingTests
{
    internal static void Run()
    {
        var parser = new A2UiPresentationParser();
        var source = Input("/events/0/a~1b/~0value", new { events = new[] {
            new Dictionary<string, object> { ["a/b"] = new Dictionary<string, string> { ["~value"] = "Resolved" } } } });
        var parsed = parser.Parse(source);
        Check(parsed.IsSuccess && ProducerInteropTests.Texts(parsed.Plan!.Surface.Root).Single().Value == "Resolved",
            "Object, array and JSON pointer escapes must resolve without flattening source keys.");
        foreach (var path in new[] { "relative", "/events/-", "/events/01/a~1b/~0value", "/events/2147483648", "/events/0/missing", "/events/0/a~2b", "/events/0/a~", "/events/0", "/events/0/a/b/c/d/e/f/g", "/" + new string('x', 1024) })
            Check(!parser.Parse(Input(path, new { events = new[] { new { title = "One" } } })).IsSuccess,
                $"Invalid or unresolved path must fail: {path}");
        foreach (var value in new object[] { 1, true, new[] { "One" }, new { nested = "One" } })
            Check(!parser.Parse(Input("/value", new { value })).IsSuccess, "Only string bindings may render.");
        foreach (var children in new object[] { new { template = new { componentId = "text" } },
            new { explicitList = "text" }, new { explicitList = new[] { "text" }, style = "red" },
            new[] { "text", "text" }, new[] { "missing" } })
        {
            var template = JsonNode.Parse(Input("/value", new { value = "One" }).Template)!;
            template["surfaceUpdate"]!["components"]![0]!["component"]!["Column"]!["children"] = JsonSerializer.SerializeToNode(children);
            Check(!parser.Parse(new(template.ToJsonString(), Input("/value", new { value = "One" }).Document)).IsSuccess,
                "Malformed, duplicate or unsupported child definitions must fail.");
        }
        foreach (var count in new[] { A2UiPresentationParser.MaximumNodes, A2UiPresentationParser.MaximumNodes + 1 })
        {
            var ids = Enumerable.Range(1, count - 1).Select(i => $"text{i}").ToArray();
            var components = new List<object> { new { id = "root", component = new { Column = new { children = new { explicitList = ids } } } } };
            components.AddRange(ids.Select(id => (object)new { id, component = new { Text = new { text = new { path = "/value" } } } }));
            var input = Input("/value", new { value = "One" }) with {
                Template = JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "binding", components } }) };
            Check(parser.Parse(input).IsSuccess == (count <= A2UiPresentationParser.MaximumNodes), "The node limit must remain enforced.");
        }
        Console.WriteLine("LegacyBindingTests: PASS (pointer resolution, malformed input, bounded nodes)");
    }

    private static PresentationInput Input(string path, object value) => new(
        JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "binding", components = new object[] {
            new { id = "root", component = new { Column = new { children = new { explicitList = new[] { "text" } } } } },
            new { id = "text", component = new { Text = new { text = new { path } } } },
        } } }), JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId = "binding", path = "/", value } }));

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
