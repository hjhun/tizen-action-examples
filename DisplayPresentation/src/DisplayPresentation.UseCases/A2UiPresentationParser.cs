using System.Text.Json;
using DisplayPresentation.Domain;

namespace DisplayPresentation.UseCases;

/// <summary>
/// Parses the deliberately small A2UI Samsung One UI profile. JSON is untrusted input;
/// successful output is a bounded immutable semantic tree, not a UI-specific render recipe.
/// </summary>
public sealed class A2UiPresentationParser
{
    public const int MaximumJsonCharacters = 64 * 1024;
    public const int MaximumNodes = 32;
    public const int MaximumDepth = 4;
    public const int MaximumDisplayCharacters = 256;
    public const int MaximumIdentifierCharacters = 64;

    private static readonly HashSet<string> TextRoles =
        ["headline", "title", "body", "label", "supporting"];

    public RenderOutcome Parse(PresentationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        if (string.IsNullOrWhiteSpace(input.Template) || string.IsNullOrWhiteSpace(input.Document) ||
            input.Template.Length > MaximumJsonCharacters || input.Document.Length > MaximumJsonCharacters)
        {
            return Invalid("Template and Document must be non-empty JSON values up to 64 KiB.");
        }

        try
        {
            using var templateJson = JsonDocument.Parse(input.Template);
            using var documentJson = JsonDocument.Parse(input.Document);
            return ParseDocuments(templateJson.RootElement, documentJson.RootElement, cancellationToken);
        }
        catch (JsonException)
        {
            return Invalid("Template and Document must be valid JSON objects.");
        }
    }

    private static RenderOutcome ParseDocuments(JsonElement template, JsonElement document, CancellationToken cancellationToken)
    {
        if (template.ValueKind != JsonValueKind.Object || document.ValueKind != JsonValueKind.Object ||
            !template.TryGetProperty("surfaceUpdate", out var surfaceUpdate) || surfaceUpdate.ValueKind != JsonValueKind.Object ||
            !document.TryGetProperty("dataModelUpdate", out var dataModelUpdate) || dataModelUpdate.ValueKind != JsonValueKind.Object ||
            !TryString(surfaceUpdate, "surfaceId", out var surfaceId) ||
            !TryString(dataModelUpdate, "surfaceId", out var documentSurfaceId) || surfaceId != documentSurfaceId ||
            !TryExactString(dataModelUpdate, "path", "/") ||
            !surfaceUpdate.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array ||
            !dataModelUpdate.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return Invalid("A matching A2UI surfaceUpdate and dataModelUpdate object is required.");
        }

        var definitions = new Dictionary<string, ComponentDefinition>(StringComparer.Ordinal);
        foreach (var item in components.EnumerateArray())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Cancelled();
            }

            if (definitions.Count >= MaximumNodes)
            {
                return Invalid($"An A2UI surface may contain at most {MaximumNodes} components.");
            }

            var definition = TryReadDefinition(item, out var parsed, out var failure) ? parsed! : null;
            if (definition is null)
            {
                return failure!;
            }

            if (!definitions.TryAdd(definition.Id, definition))
            {
                return Invalid($"Component ID '{definition.Id}' is duplicated.");
            }
        }

        if (definitions.Count == 0)
        {
            return Unsupported("The A2UI surface contains no supported components.");
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions.Values.OfType<ColumnDefinition>())
        {
            foreach (var childId in definition.Children)
            {
                if (!definitions.ContainsKey(childId))
                {
                    return Invalid($"Column '{definition.Id}' references unknown component '{childId}'.");
                }
                if (!referenced.Add(childId))
                {
                    return Invalid($"Component '{childId}' has more than one parent.");
                }
            }
        }

        var roots = definitions.Values.Where(definition => !referenced.Contains(definition.Id)).ToArray();
        if (roots.Length != 1 || roots[0] is not ColumnDefinition rootDefinition)
        {
            return Invalid("A surface must contain exactly one unreferenced Column root.");
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var root = BuildNode(rootDefinition, definitions, value, 1, visiting, visited, cancellationToken, out var buildFailure);
        if (root is null)
        {
            return buildFailure!;
        }
        if (visited.Count != definitions.Count)
        {
            return Invalid("Every A2UI component must be reachable from the root Column.");
        }

        return RenderOutcome.Success(new RenderPlan(new SemanticSurface(surfaceId, root)));
    }

    private static SemanticNode? BuildNode(
        ComponentDefinition definition,
        IReadOnlyDictionary<string, ComponentDefinition> definitions,
        JsonElement value,
        int depth,
        ISet<string> visiting,
        ISet<string> visited,
        CancellationToken cancellationToken,
        out RenderOutcome? failure)
    {
        failure = null;
        if (cancellationToken.IsCancellationRequested)
        {
            failure = Cancelled();
            return null;
        }
        if (depth > MaximumDepth)
        {
            failure = Invalid($"A2UI nesting may not exceed {MaximumDepth} levels.");
            return null;
        }
        if (!visiting.Add(definition.Id))
        {
            failure = Invalid("A2UI component children may not form a cycle.");
            return null;
        }

        try
        {
            if (definition is TextDefinition text)
            {
                if (!TryValueAtRootPath(value, text.Path, out var displayValue))
                {
                    failure = Invalid($"Document does not provide a string for '{text.Path}'.");
                    return null;
                }
                visited.Add(text.Id);
                return new TextValue(text.Id, text.Role, Bound(displayValue));
            }

            var column = (ColumnDefinition)definition;
            var children = new List<SemanticNode>(column.Children.Count);
            foreach (var childId in column.Children)
            {
                var child = BuildNode(definitions[childId], definitions, value, depth + 1, visiting, visited, cancellationToken, out failure);
                if (child is null)
                {
                    return null;
                }
                children.Add(child);
            }
            visited.Add(column.Id);
            return new VerticalGroup(column.Id, children);
        }
        finally
        {
            visiting.Remove(definition.Id);
        }
    }

    private static bool TryReadDefinition(JsonElement item, out ComponentDefinition? definition, out RenderOutcome? failure)
    {
        definition = null;
        failure = null;
        if (item.ValueKind != JsonValueKind.Object || !TryIdentifier(item, "id", out var id) ||
            !item.TryGetProperty("component", out var component) || component.ValueKind != JsonValueKind.Object ||
            component.EnumerateObject().Count() != 1)
        {
            failure = Invalid("Each A2UI component needs a bounded ID and exactly one component type.");
            return false;
        }

        var type = component.EnumerateObject().Single();
        if (type.NameEquals("Column"))
        {
            if (type.Value.ValueKind != JsonValueKind.Object || !HasOnlyProperties(type.Value, "children") ||
                !TryIdentifiers(type.Value, "children", out var children))
            {
                failure = Unsupported("Column supports only an ordered children ID array.");
                return false;
            }
            definition = new ColumnDefinition(id, children);
            return true;
        }

        if (type.NameEquals("Text"))
        {
            if (type.Value.ValueKind != JsonValueKind.Object || !HasOnlyProperties(type.Value, "text", "role") ||
                !type.Value.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(text, "path") || !TryString(text, "path", out var path) ||
                !IsRootPath(path))
            {
                failure = Unsupported("Text supports only a root string text.path and optional profile role.");
                return false;
            }

            var role = "body";
            if (type.Value.TryGetProperty("role", out var roleValue) &&
                (roleValue.ValueKind != JsonValueKind.String || !TextRoles.Contains(role = roleValue.GetString() ?? string.Empty)))
            {
                failure = Unsupported("Text role is not supported by A2UI Samsung One UI Profile v0.1.");
                return false;
            }
            definition = new TextDefinition(id, path, role);
            return true;
        }

        failure = Unsupported($"Unsupported A2UI component '{type.Name}'.");
        return false;
    }

    private static bool TryIdentifiers(JsonElement element, string property, out IReadOnlyList<string> identifiers)
    {
        identifiers = Array.Empty<string>();
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var result = new List<string>();
        foreach (var child in array.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.String || !IsIdentifier(child.GetString()))
            {
                return false;
            }
            result.Add(child.GetString()!);
        }
        identifiers = result;
        return true;
    }

    private static bool TryIdentifier(JsonElement element, string property, out string identifier) =>
        TryString(element, property, out identifier) && IsIdentifier(identifier);

    private static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumIdentifierCharacters && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':');

    private static bool HasOnlyProperties(JsonElement element, params string[] allowed) =>
        element.EnumerateObject().All(property => allowed.Contains(property.Name, StringComparer.Ordinal));

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = child.GetString() ?? string.Empty);
    }

    private static bool TryExactString(JsonElement element, string property, string expected) =>
        element.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.String && child.GetString() == expected;

    private static bool IsRootPath(string path) => path.StartsWith("/", StringComparison.Ordinal) && path.Length > 1 &&
        !path[1..].Contains('/');

    private static bool TryValueAtRootPath(JsonElement value, string path, out string displayValue)
    {
        displayValue = string.Empty;
        return value.TryGetProperty(path[1..], out var item) && item.ValueKind == JsonValueKind.String &&
            (displayValue = item.GetString() ?? string.Empty) is not null;
    }

    private static string Bound(string value) => value.Length <= MaximumDisplayCharacters ? value : value[..MaximumDisplayCharacters];

    private static RenderOutcome Invalid(string message) => RenderOutcome.Fail(RenderFailureKind.InvalidInput, message);
    private static RenderOutcome Unsupported(string message) => RenderOutcome.Fail(RenderFailureKind.Unsupported, message);
    private static RenderOutcome Cancelled() => RenderOutcome.Fail(RenderFailureKind.Cancelled, "Rendering was cancelled.");

    private abstract record ComponentDefinition(string Id);
    private sealed record ColumnDefinition(string Id, IReadOnlyList<string> Children) : ComponentDefinition(Id);
    private sealed record TextDefinition(string Id, string Path, string Role) : ComponentDefinition(Id);
}
