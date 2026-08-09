using System.Text.Json;
using DisplayPresentation.Domain;

namespace DisplayPresentation.UseCases;

public sealed class A2UiPresentationParser
{
    public const int MaximumJsonCharacters = 64 * 1024;
    public const int MaximumFields = 32;
    public const int MaximumDisplayCharacters = 256;

    public RenderOutcome Parse(PresentationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cancellationToken.IsCancellationRequested)
        {
            return RenderOutcome.Fail(RenderFailureKind.Cancelled, "Rendering was cancelled.");
        }

        if (string.IsNullOrWhiteSpace(input.Template) || string.IsNullOrWhiteSpace(input.Document) ||
            input.Template.Length > MaximumJsonCharacters || input.Document.Length > MaximumJsonCharacters)
        {
            return RenderOutcome.Fail(RenderFailureKind.InvalidInput, "Template and Document must be non-empty JSON values up to 64 KiB.");
        }

        try
        {
            using var templateJson = JsonDocument.Parse(input.Template);
            using var documentJson = JsonDocument.Parse(input.Document);
            return ParseDocuments(templateJson.RootElement, documentJson.RootElement, cancellationToken);
        }
        catch (JsonException)
        {
            return RenderOutcome.Fail(RenderFailureKind.InvalidInput, "Template and Document must be valid JSON objects.");
        }
    }

    private static RenderOutcome ParseDocuments(JsonElement template, JsonElement document, CancellationToken cancellationToken)
    {
        if (template.ValueKind != JsonValueKind.Object || document.ValueKind != JsonValueKind.Object ||
            !template.TryGetProperty("surfaceUpdate", out var surfaceUpdate) ||
            !document.TryGetProperty("dataModelUpdate", out var dataModelUpdate) ||
            surfaceUpdate.ValueKind != JsonValueKind.Object || dataModelUpdate.ValueKind != JsonValueKind.Object ||
            !TryString(surfaceUpdate, "surfaceId", out var templateSurfaceId) ||
            !TryString(dataModelUpdate, "surfaceId", out var documentSurfaceId) ||
            templateSurfaceId != documentSurfaceId ||
            !surfaceUpdate.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array ||
            !dataModelUpdate.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return RenderOutcome.Fail(RenderFailureKind.InvalidInput, "A matching A2UI surfaceUpdate and dataModelUpdate object is required.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in components.EnumerateArray())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return RenderOutcome.Fail(RenderFailureKind.Cancelled, "Rendering was cancelled.");
            }

            if (!item.TryGetProperty("component", out var component) || component.ValueKind != JsonValueKind.Object || component.EnumerateObject().Count() != 1)
            {
                return RenderOutcome.Fail(RenderFailureKind.InvalidInput, "Each A2UI component must define exactly one component type.");
            }

            var type = component.EnumerateObject().Single();
            if (type.NameEquals("Column"))
            {
                continue;
            }

            if (!type.NameEquals("Text") || !type.Value.TryGetProperty("text", out var text) ||
                text.ValueKind != JsonValueKind.Object || !text.TryGetProperty("path", out var path) ||
                path.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(path.GetString()))
            {
                return RenderOutcome.Fail(RenderFailureKind.Unsupported, $"Unsupported A2UI component '{type.Name}'.");
            }

            paths.Add(path.GetString()!);
        }

        if (paths.Count == 0)
        {
            return RenderOutcome.Fail(RenderFailureKind.Unsupported, "The A2UI surface contains no supported Text components.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (!TryValueAtRootPath(value, path, out var displayValue))
            {
                return RenderOutcome.Fail(RenderFailureKind.InvalidInput, $"Document does not provide a string for '{path}'.");
            }
            values[path] = Bound(displayValue);
        }

        var title = values.TryGetValue("/title", out var titleValue) ? titleValue : values.First().Value;
        var subtitle = values.TryGetValue("/subtitle", out var subtitleValue) ? subtitleValue : string.Empty;
        var body = values.TryGetValue("/body", out var bodyValue) ? bodyValue : string.Empty;
        var fields = values.Where(item => item.Key is not "/title" and not "/subtitle" and not "/body")
            .Take(MaximumFields)
            .Select(item => new RenderField(item.Key.TrimStart('/'), item.Value))
            .ToArray();
        return RenderOutcome.Success(new RenderPlan(templateSurfaceId, title, subtitle, body, fields));
    }

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = child.GetString() ?? string.Empty);
    }

    private static bool TryValueAtRootPath(JsonElement value, string path, out string displayValue)
    {
        displayValue = string.Empty;
        if (!path.StartsWith("/", StringComparison.Ordinal) || path.Length == 1 || path[1..].Contains('/'))
        {
            return false;
        }
        return value.TryGetProperty(path[1..], out var item) && item.ValueKind == JsonValueKind.String &&
            (displayValue = item.GetString() ?? string.Empty) is not null;
    }

    private static string Bound(string value) => value.Length <= MaximumDisplayCharacters ? value : value[..MaximumDisplayCharacters];
}
