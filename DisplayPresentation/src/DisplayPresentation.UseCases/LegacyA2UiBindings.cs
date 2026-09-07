using System.Text.Json;

namespace DisplayPresentation.UseCases;

/// <summary>
/// Binding forms for the repository's split legacy v0.8 compatibility transport.
/// Array children are retained for older repository fixtures; no canonical lifecycle
/// or catalog is inferred from either representation.
/// </summary>
internal static class LegacyA2UiBindings
{
    internal static bool TryChildren(JsonElement column, out JsonElement children)
    {
        children = default;
        if (!column.TryGetProperty("children", out var value)) return false;
        if (value.ValueKind == JsonValueKind.Array)
        {
            children = value;
            return true;
        }
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 1 ||
            !value.TryGetProperty("explicitList", out children) || children.ValueKind != JsonValueKind.Array)
            return false;
        return true;
    }

    internal static bool IsPath(string path) => TrySegments(path, out _);

    internal static bool TryResolveString(JsonElement model, string path, out string value)
    {
        value = string.Empty;
        if (!TrySegments(path, out var segments)) return false;
        var current = model;
        foreach (var segment in segments)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current)) return false;
            }
            else if (current.ValueKind == JsonValueKind.Array &&
                (segment == "0" || (segment.Length > 0 && segment[0] is >= '1' and <= '9')) &&
                segment.All(char.IsAsciiDigit) && int.TryParse(segment, out var index) && index < current.GetArrayLength())
                current = current[index];
            else return false;
        }
        if (current.ValueKind != JsonValueKind.String) return false;
        value = current.GetString()!;
        return true;
    }

    private static bool TrySegments(string path, out string[] segments)
    {
        segments = [];
        if (path.Length is < 2 or > 1024 || path[0] != '/') return false;
        var encoded = path[1..].Split('/');
        if (encoded.Length > 8) return false;
        for (var i = 0; i < encoded.Length; i++)
        {
            var segment = encoded[i];
            for (var j = 0; j < segment.Length; j++)
                if (segment[j] == '~' && (++j == segment.Length || segment[j] is not ('0' or '1'))) return false;
            encoded[i] = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        }
        segments = encoded;
        return true;
    }
}
