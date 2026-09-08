using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DisplayPresentation.UseCases;

public enum CanonicalProjectionStatus { MissingSurface, WaitingForRoot, PartialProjection, Projected, ProjectionLimitExceeded, UnsupportedBindingValue, UnsupportedBindingPath, InvalidBindingPath }
public sealed record CanonicalProjectionResult(CanonicalProjectionStatus Status, string SurfaceId, CanonicalProjectedNode? Root)
{
    public JsonValueKind? BindingValueKind { get; init; }
}
public abstract record CanonicalProjectedNode(string SourceId, IReadOnlyList<int> OccurrencePath);
public sealed record CanonicalProjectedText(string SourceId, IReadOnlyList<int> OccurrencePath, string Text, string? Variant)
    : CanonicalProjectedNode(SourceId, OccurrencePath);
public sealed record CanonicalProjectedColumn(string SourceId, IReadOnlyList<int> OccurrencePath, string? Justify, string? Align,
    IReadOnlyList<CanonicalProjectedNode> Children) : CanonicalProjectedNode(SourceId, OccurrencePath);
public sealed record CanonicalUnresolvedChild(string SourceId, IReadOnlyList<int> OccurrencePath)
    : CanonicalProjectedNode(SourceId, OccurrencePath);

public enum CanonicalBindingResolution { Resolved, Missing, Uninitialized }
public sealed record CanonicalProjectedBoundText(string SourceId, IReadOnlyList<int> OccurrencePath,
    string Text, string? Variant, string BindingPath) : CanonicalProjectedNode(SourceId, OccurrencePath)
{
    public CanonicalBindingResolution Resolution => CanonicalBindingResolution.Resolved;
}
public sealed record CanonicalPendingBinding(string SourceId, IReadOnlyList<int> OccurrencePath,
    string? Variant, string BindingPath, CanonicalBindingResolution Resolution) : CanonicalProjectedNode(SourceId, OccurrencePath);

/// <summary>Consumes atomically captured components and Data; emits only referenced text. No legacy/NUI or arbitrary public snapshot input.</summary>
internal static class CanonicalSurfaceProjector
{
    // Local output limits, counting repeated occurrences AND unresolved edge slots.
    private const int MaximumSlots = 256, MaximumDepth = 32, MaximumBytes = 65536;

    internal static CanonicalProjectionResult Project(string surfaceId, IReadOnlyList<JsonElement>? components, JsonElement? data)
    {
        var nodes = components?.ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        int emitted = 0;
        bool partial = false;
        try
        {
            using var sink = new ProjectionByteCounter();
            using var writer = new Utf8JsonWriter(sink, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default });
            // Resource-only compact UTF8 form, NOT a wire serializer:
            // {surfaceId,root,status}; each node {kind,sourceId,path,...}.
            // Text adds text then optional variant; Column optional justify/align then children.
            // Unresolved adds nothing. Null optionals are omitted; paths are numeric arrays.
            // Every field, key, repeated string/child, escape and status counts, including derived
            // fields explicitly written below. No polymorphic base-record serialization.
            writer.WriteStartObject();
            writer.WriteString("surfaceId", surfaceId);
            writer.WritePropertyName("root");
            writer.Flush();
            CanonicalProjectedNode? root = null;
            var status = nodes is null ? CanonicalProjectionStatus.MissingSurface : CanonicalProjectionStatus.WaitingForRoot;
            if (nodes is not null && nodes.ContainsKey("root"))
            {
                root = Visit("root", Array.Empty<int>(), null);
                status = partial ? CanonicalProjectionStatus.PartialProjection : CanonicalProjectionStatus.Projected;
            }
            else writer.WriteNullValue();
            writer.WriteString("status", status.ToString());
            writer.WriteEndObject();
            writer.Flush();
            return new(status, surfaceId, root);

            CanonicalProjectedNode Visit(string id, IReadOnlyList<int> parentPath, int? childIndex)
            {
                int depth = parentPath.Count + (childIndex.HasValue ? 2 : 1);
                // Check before occurrence path/node allocation or recursive descent into children.
                if (emitted >= MaximumSlots || depth > MaximumDepth) throw new ProjectionLimitException();
                emitted++;
                var pathArray = new int[depth - 1];
                for (int i = 0; i < parentPath.Count; i++) pathArray[i] = parentPath[i];
                if (childIndex.HasValue) pathArray[^1] = childIndex.Value;
                var path = Array.AsReadOnly(pathArray);
                bool found = nodes!.TryGetValue(id, out var node);
                string kind = found ? node.GetProperty("component").GetString()! : "Unresolved";
                writer.WriteStartObject();
                writer.WriteString("kind", kind);
                writer.WriteString("sourceId", id);
                writer.WriteStartArray("path");
                foreach (var index in path) writer.WriteNumberValue(index);
                writer.WriteEndArray();
                writer.Flush();
                CanonicalProjectedNode result;
                if (!found)
                {
                    partial = true;
                    result = new CanonicalUnresolvedChild(id, path);
                }
                else if (kind == "Text")
                {
                    var input = node.GetProperty("text");
                    string? bindingPath = input.ValueKind == JsonValueKind.Object ? input.GetProperty("path").GetString()! : null;
                    var resolution = CanonicalBindingResolution.Resolved;
                    var text = bindingPath is null ? input.GetString()! : Resolve(bindingPath, data, out resolution);
                    var variant = Optional(node, "variant");
                    if (resolution == CanonicalBindingResolution.Resolved) writer.WriteString("text", text);
                    if (variant is not null) writer.WriteString("variant", variant);
                    // Preserve literal resource format; bound nodes add exact provenance/status.
                    // Pending text omits text but includes optional variant and both binding fields.
                    if (bindingPath is not null)
                    {
                        writer.WriteString("bindingPath", bindingPath);
                        writer.WriteString("resolution", resolution.ToString());
                    }
                    writer.Flush(); // Count long/repeated encoded text before expanding further.
                    if (bindingPath is null) result = new CanonicalProjectedText(id, path, text, variant);
                    else if (resolution == CanonicalBindingResolution.Resolved)
                        result = new CanonicalProjectedBoundText(id, path, text, variant, bindingPath);
                    else
                    {
                        partial = true;
                        result = new CanonicalPendingBinding(id, path, variant, bindingPath, resolution);
                    }
                }
                else
                {
                    var justify = Optional(node, "justify");
                    var align = Optional(node, "align");
                    if (justify is not null) writer.WriteString("justify", justify);
                    if (align is not null) writer.WriteString("align", align);
                    writer.WriteStartArray("children");
                    writer.Flush();
                    var children = new List<CanonicalProjectedNode>();
                    int index = 0;
                    foreach (var child in node.GetProperty("children").EnumerateArray())
                        children.Add(Visit(child.GetString()!, path, index++));
                    writer.WriteEndArray();
                    result = new CanonicalProjectedColumn(id, path, justify, align, children.AsReadOnly());
                }
                writer.WriteEndObject();
                writer.Flush();
                return result;
            }
        }
        catch (BindingException e)
        {
            return new(e.Status, surfaceId, null) { BindingValueKind = e.Kind };
        }
        catch (ProjectionLimitException)
        {
            // PartialProjection means missing edges/bindings only, never truncation. No accepted root.
            return new(CanonicalProjectionStatus.ProjectionLimitExceeded, surfaceId, null);
        }
    }

    private static string Resolve(string path, JsonElement? data, out CanonicalBindingResolution resolution)
    {
        // Local precedence: UTF8 bytes -> token count -> entire escape syntax -> supported
        // absolute subset -> lookup. Missing data cannot hide malformed trailing escapes.
        if (Encoding.UTF8.GetByteCount(path) > 1024) throw new ProjectionLimitException();
        var tokens = path.Split('/');
        if (tokens.Length - (path.StartsWith('/') ? 1 : 0) > 32) throw new ProjectionLimitException();
        for (int i = 0; i < path.Length; i++)
            if (path[i] == '~' && (++i == path.Length || path[i] is not ('0' or '1')))
                throw new BindingException(CanonicalProjectionStatus.InvalidBindingPath);
        if (!path.StartsWith('/') || path.Length == 1)
            throw new BindingException(CanonicalProjectionStatus.UnsupportedBindingPath);
        resolution = data.HasValue ? CanonicalBindingResolution.Missing : CanonicalBindingResolution.Uninitialized;
        if (!data.HasValue) return "";
        var current = data.Value;
        foreach (var token in tokens.Skip(1))
        {
            if (current.ValueKind != JsonValueKind.Object)
                throw new BindingException(CanonicalProjectionStatus.UnsupportedBindingPath);
            var key = token.Replace("~1", "/").Replace("~0", "~");
            if (!current.TryGetProperty(key, out current)) return "";
        }
        if (current.ValueKind != JsonValueKind.String)
            throw new BindingException(CanonicalProjectionStatus.UnsupportedBindingValue, current.ValueKind);
        resolution = CanonicalBindingResolution.Resolved;
        return current.GetString()!;
    }

    private sealed class BindingException(CanonicalProjectionStatus status, JsonValueKind? kind = null) : Exception
    {
        internal CanonicalProjectionStatus Status => status;
        internal JsonValueKind? Kind => kind;
    }

    private static string? Optional(JsonElement node, string key) => node.TryGetProperty(key, out var v) ? v.GetString() : null;
    private sealed class ProjectionLimitException : Exception { }
    // Measurement bounds serialized output, not JsonElement metadata/clones/transient/total RAM.
    private sealed class ProjectionByteCounter : Stream
    {
        private int _length;
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > MaximumBytes - _length) throw new ProjectionLimitException();
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
