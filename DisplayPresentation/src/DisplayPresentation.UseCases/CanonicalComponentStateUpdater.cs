using System.Text.Encodings.Web;
using System.Text.Json;

namespace DisplayPresentation.UseCases;

/// <summary>Literal Column/Text graph state, not semantic rendering or full catalog validation.</summary>
internal static class CanonicalComponentStateUpdater
{
    // Local cumulative component policies; distinct from C0 input limits and C2 Data.
    private const int MaximumRecords = 256, MaximumEdges = 1024, MaximumBytes = 65536, MaximumDepth = 32;
    // Pinned basic catalog at 8ff4651232ab0e02b0123730b502711170637a3a (inspected 2026-09-08).
    private static readonly string[] Variants = ["h1", "h2", "h3", "h4", "h5", "caption", "body"];
    private static readonly string[] Justify = ["start", "center", "end", "spaceBetween", "spaceAround", "spaceEvenly", "stretch"];
    private static readonly string[] Align = ["center", "end", "start", "stretch"];

    internal static CanonicalSurfaceApplyStatus Apply(IReadOnlyList<JsonElement> current, JsonElement batch,
        out IReadOnlyList<JsonElement>? updated)
    {
        updated = null;
        // C0 + surface lookup precede: batch cardinality; shape then duplicate per input item;
        // same-ID policy; cumulative count/edges/bytes; cycle then longest known-node path.
        if (batch.GetArrayLength() > MaximumRecords) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        var incoming = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var node in batch.EnumerateArray())
        {
            var status = Validate(node);
            if (status != CanonicalSurfaceApplyStatus.ComponentsUpdated) return status;
            if (!incoming.TryAdd(node.GetProperty("id").GetString()!, node))
                return CanonicalSurfaceApplyStatus.DuplicateComponentId;
        }
        var candidate = current.ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        foreach (var (id, node) in incoming)
        {
            if (candidate.TryGetValue(id, out var old) && !SameRecord(old, node))
                return CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate;
        }
        foreach (var (id, node) in incoming) candidate.TryAdd(id, node); // Exact repeat preserves stored record.
        if (candidate.Count > MaximumRecords) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        var edges = candidate.ToDictionary(x => x.Key, x => Children(x.Value), StringComparer.Ordinal);
        if (edges.Values.Sum(x => x.Length) > MaximumEdges) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        var ordered = candidate.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value).ToArray();
        try
        {
            using var sink = new BoundedComponentStream();
            using (var writer = new Utf8JsonWriter(sink, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            {
                writer.WriteStartArray();
                foreach (var node in ordered) node.WriteTo(writer);
                writer.WriteEndArray();
                writer.Flush();
            }
        }
        catch (ComponentLimitException) { return CanonicalSurfaceApplyStatus.StateLimitExceeded; }

        // Visit every connected/disconnected known node. Missing targets are not placeholder nodes.
        var heights = new Dictionary<string, int>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        bool cycle = false;
        int Height(string id)
        {
            if (!edges.ContainsKey(id)) return 0;
            if (heights.TryGetValue(id, out var height)) return height;
            if (!active.Add(id)) { cycle = true; return 0; }
            int longest = 0;
            foreach (var child in edges[id]) longest = Math.Max(longest, Height(child));
            active.Remove(id);
            return heights[id] = longest + 1;
        }
        foreach (var id in edges.Keys) Height(id);
        if (cycle) return CanonicalSurfaceApplyStatus.ComponentCycle;
        if (heights.Values.Any(x => x > MaximumDepth)) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        updated = Array.AsReadOnly(ordered.Select(x => x.Clone()).ToArray());
        return CanonicalSurfaceApplyStatus.ComponentsUpdated;
    }

    private static CanonicalSurfaceApplyStatus Validate(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object || !String(node, "id") || !String(node, "component"))
            return CanonicalSurfaceApplyStatus.InvalidComponent;
        var kind = node.GetProperty("component").GetString();
        if (kind is not ("Column" or "Text")) return CanonicalSurfaceApplyStatus.UnsupportedComponent;
        var required = kind == "Text" ? "text" : "children";
        if (!node.TryGetProperty(required, out var value)) return CanonicalSurfaceApplyStatus.InvalidComponent;
        string[] allowed = kind == "Text" ? ["id", "component", "text", "variant", "weight", "accessibility"]
            : ["id", "component", "children", "justify", "align", "weight", "accessibility"];
        if (node.EnumerateObject().Any(x => !allowed.Contains(x.Name))) return CanonicalSurfaceApplyStatus.InvalidComponent;
        if ((kind == "Text" && !OptionalEnum(node, "variant", Variants)) ||
            (kind == "Column" && (!OptionalEnum(node, "justify", Justify) || !OptionalEnum(node, "align", Align))))
            return CanonicalSurfaceApplyStatus.InvalidComponent;
        // Recognizing a catalog-owned form is not certifying that form's schema validity.
        if (node.TryGetProperty("weight", out _) || node.TryGetProperty("accessibility", out _) || value.ValueKind == JsonValueKind.Object)
            return CanonicalSurfaceApplyStatus.UnsupportedComponentForm;
        if (kind == "Text" ? value.ValueKind != JsonValueKind.String :
            value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))
            return CanonicalSurfaceApplyStatus.InvalidComponent;
        return CanonicalSurfaceApplyStatus.ComponentsUpdated;
    }

    private static bool String(JsonElement node, string key) =>
        node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String;
    private static bool OptionalEnum(JsonElement node, string key, string[] values) =>
        !node.TryGetProperty(key, out var value) || (value.ValueKind == JsonValueKind.String && values.Contains(value.GetString()));
    private static string[] Children(JsonElement node) => node.GetProperty("component").GetString() == "Column"
        ? node.GetProperty("children").EnumerateArray().Select(x => x.GetString()!).ToArray() : [];

    private static bool SameRecord(JsonElement first, JsonElement second)
    {
        // Admitted values are strings or arrays of strings. Ignore object property order only;
        // do not default absent optional values, normalize literals or reorder/deduplicate edges.
        if (first.EnumerateObject().Count() != second.EnumerateObject().Count()) return false;
        foreach (var p in first.EnumerateObject())
        {
            if (!second.TryGetProperty(p.Name, out var value) || value.ValueKind != p.Value.ValueKind) return false;
            if (value.ValueKind == JsonValueKind.String)
            {
                if (value.GetString() != p.Value.GetString()) return false;
            }
            else if (!value.EnumerateArray().Select(x => x.GetString()).SequenceEqual(p.Value.EnumerateArray().Select(x => x.GetString()))) return false;
        }
        return true;
    }

    private sealed class ComponentLimitException : Exception { }
    // Count compact Default-encoded sorted-array bytes, without retaining a whole output string.
    // Retained payload bounds exclude Data/create bodies, caller snapshots and transient memory.
    private sealed class BoundedComponentStream : Stream
    {
        private int _length;
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > MaximumBytes - _length) throw new ComponentLimitException();
            _length += buffer.Length;
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
