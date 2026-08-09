using System.Text.Json;
using DisplayPresentation.Domain;

namespace DisplayPresentation.UseCases;

/// <summary>
/// Recreates the profile-owned A2UI wire pair from the accepted semantic tree. This is the sole
/// View round-trip path, so bounded resolved values rather than untrusted source JSON are stored.
/// </summary>
public static class A2UiPresentationSerializer
{
    public static PresentationInput Serialize(SemanticSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var components = new List<object>();
        var value = new Dictionary<string, string>(StringComparer.Ordinal);
        AddNode(surface.Root, components, value);
        return new PresentationInput(
            JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = surface.SurfaceId, components } }),
            JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId = surface.SurfaceId, path = "/", value } }));
    }

    private static void AddNode(SemanticNode node, ICollection<object> components, IDictionary<string, string> value)
    {
        switch (node)
        {
            case VerticalGroup group:
                components.Add(new { id = group.Id, component = new { Column = new { children = group.Children.Select(child => child.Id).ToArray() } } });
                foreach (var child in group.Children)
                {
                    AddNode(child, components, value);
                }
                return;
            case TextValue text:
                var field = text.Id;
                value.Add(field, text.Value);
                components.Add(new { id = text.Id, component = new { Text = new { text = new { path = $"/{field}" }, role = text.Role } } });
                return;
            default:
                throw new InvalidOperationException("Only profile-validated semantic nodes may be serialized.");
        }
    }
}