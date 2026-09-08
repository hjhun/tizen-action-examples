using System.Text;
using System.Text.Json;

namespace DisplayPresentation.UseCases;

public enum CanonicalEnvelopeReadStatus { Recognized, UnsupportedVersion, InvalidEnvelope }
public enum CanonicalA2UiMessageKind { CreateSurface, UpdateComponents, UpdateDataModel, DeleteSurface }

/// <summary>Body owns a cloned JSON value; it remains usable after the reader disposes its document.</summary>
public sealed record CanonicalA2UiEnvelope(string Version, CanonicalA2UiMessageKind Kind, JsonElement Body);
public sealed record CanonicalEnvelopeReadResult(CanonicalEnvelopeReadStatus Status, CanonicalA2UiEnvelope? Envelope);

/// <summary>
/// Reads one already framed envelope for the selected v0.9.1 profile. Recognition is not
/// catalog validation, lifecycle application, rendering admission or transport negotiation.
/// The legacy parser, serializer and provider do not use this reader.
/// </summary>
public static class CanonicalA2UiMessageReader
{
    // Local resource/input policies, not limits imposed by the upstream A2UI specification.
    public const int MaximumUtf8Bytes = 64 * 1024;
    public const int MaximumDepth = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static CanonicalEnvelopeReadResult Read(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length == 0 || utf8.Length > MaximumUtf8Bytes) return Invalid();
        try
        {
            // Validate all bytes, including strings that this boundary deliberately does not interpret.
            StrictUtf8.GetCharCount(utf8.Span);
            if (!HasValidUnicodeStrings(utf8.Span)) return Invalid();
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = MaximumDepth });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root) ||
                !HasString(root, "version")) return Invalid();
            var version = root.GetProperty("version").GetString()!;
            // Do not apply the selected profile's body schema to a different protocol version.
            // UnsupportedVersion does not assert whether that other version's envelope is valid.
            if (version != "v0.9.1")
                return new(CanonicalEnvelopeReadStatus.UnsupportedVersion, null);

            CanonicalA2UiMessageKind? kind = null;
            JsonElement body = default;
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "version") continue;
                if (kind is not null) return Invalid();
                kind = property.Name switch
                {
                    "createSurface" => CanonicalA2UiMessageKind.CreateSurface,
                    "updateComponents" => CanonicalA2UiMessageKind.UpdateComponents,
                    "updateDataModel" => CanonicalA2UiMessageKind.UpdateDataModel,
                    "deleteSurface" => CanonicalA2UiMessageKind.DeleteSurface,
                    _ => null,
                };
                if (kind is null) return Invalid();
                body = property.Value;
            }
            if (kind is null || !ValidBody(kind.Value, body)) return Invalid();
            return new(CanonicalEnvelopeReadStatus.Recognized, new(version, kind.Value, body.Clone()));
        }
        catch (JsonException) { return Invalid(); }
        catch (DecoderFallbackException) { return Invalid(); }
    }

    private static bool ValidBody(CanonicalA2UiMessageKind kind, JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || !HasString(body, "surfaceId")) return false;
        // Literal strings are retained, including empty/whitespace IDs. No legacy regex or URL alias.
        string[] allowed = kind switch
        {
            CanonicalA2UiMessageKind.CreateSurface => ["surfaceId", "catalogId", "theme", "sendDataModel"],
            CanonicalA2UiMessageKind.UpdateComponents => ["surfaceId", "components"],
            CanonicalA2UiMessageKind.UpdateDataModel => ["surfaceId", "path", "value"],
            CanonicalA2UiMessageKind.DeleteSurface => ["surfaceId"],
            _ => [],
        };
        if (body.EnumerateObject().Any(property => !allowed.Contains(property.Name))) return false;
        switch (kind)
        {
            case CanonicalA2UiMessageKind.CreateSurface:
                // theme is catalog-owned: retain it without interpreting or applying its schema.
                return HasString(body, "catalogId") &&
                    (!body.TryGetProperty("sendDataModel", out var send) ||
                        send.ValueKind is JsonValueKind.True or JsonValueKind.False);
            case CanonicalA2UiMessageKind.UpdateComponents:
                // Component internals (including their types/properties) require later catalog validation.
                return body.TryGetProperty("components", out var components) &&
                    components.ValueKind == JsonValueKind.Array && components.GetArrayLength() >= 1;
            case CanonicalA2UiMessageKind.UpdateDataModel:
                // An absent value deletes; explicit null/primitive/object/array values remain unchanged.
                return !body.TryGetProperty("path", out var path) || path.ValueKind == JsonValueKind.String;
            case CanonicalA2UiMessageKind.DeleteSurface:
                return true;
            default:
                return false;
        }
    }

    private static bool HasString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String;

    private static bool HasValidUnicodeStrings(ReadOnlySpan<byte> utf8)
    {
        // Local rejection policy for unpaired/malformed surrogate escapes, also in opaque
        // catalog/data values. Valid UTF8 bytes alone do not validate ASCII JSON escapes.
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = MaximumDepth });
        while (reader.Read())
        {
            if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName)) continue;
            // These token types are established above. GetString can still throw when
            // unescaping malformed UTF-16; contain only that input-decoding operation.
            try { _ = reader.GetString(); }
            catch (InvalidOperationException) { return false; }
        }
        return true;
    }

    private static bool HasDuplicateProperties(JsonElement value)
    {
        // Reject duplicates throughout the envelope, including opaque data/catalog values, as a
        // local ambiguity policy. JsonDocument has already bounded nesting before this traversal.
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                if (HasDuplicateProperties(item)) return true;
        }
        return false;
    }

    private static CanonicalEnvelopeReadResult Invalid() => new(CanonicalEnvelopeReadStatus.InvalidEnvelope, null);
}
