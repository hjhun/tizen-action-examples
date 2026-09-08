using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DisplayPresentation.UseCases;

/// <summary>Local subset: root replacement and object-ancestor leaf upsert/delete only.</summary>
internal static class CanonicalDataModelUpdater
{
    // Local cumulative Data limits, separate from C0's incoming envelope bounds.
    internal const int MaximumBytes = 64 * 1024;
    internal const int MaximumDepth = 32;
    internal const int MaximumPathBytes = 1024;
    internal const int MaximumTokens = 32;

    internal static CanonicalSurfaceApplyStatus Apply(JsonElement? current, JsonElement body, out JsonElement? updated)
    {
        updated = null;
        var path = body.TryGetProperty("path", out var p) ? p.GetString()! : "/";
        // Registry checks surface existence first. Then path bytes, token count, full syntax,
        // supported operation/ancestors, candidate depth, and encoded bytes, in that order.
        if (Encoding.UTF8.GetByteCount(path) > MaximumPathBytes) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        var tokens = path.Split('/').Skip(1).ToArray();
        if (tokens.Length > MaximumTokens) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        if (path.Length != 0 && path[0] != '/') return CanonicalSurfaceApplyStatus.InvalidPath;
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            for (int j = 0; j < token.Length; j++)
                if (token[j] == '~' && (++j == token.Length || token[j] is not ('0' or '1')))
                    return CanonicalSurfaceApplyStatus.InvalidPath;
            tokens[i] = token.Replace("~1", "/").Replace("~0", "~");
        }
        bool hasValue = body.TryGetProperty("value", out var value);
        if (path.Length == 0 || (path == "/" && !hasValue)) return CanonicalSurfaceApplyStatus.UnsupportedOperation;
        JsonNode? candidate;
        if (path == "/") candidate = JsonNode.Parse(value.GetRawText());
        else
        {
            candidate = current.HasValue ? JsonNode.Parse(current.Value.GetRawText()) : null;
            JsonNode? parent = candidate;
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (parent is not JsonObject obj || !obj.TryGetPropertyValue(tokens[i], out parent))
                    return CanonicalSurfaceApplyStatus.UnsupportedOperation;
            }
            // Arrays are opaque values in this subset; no index, append, holes or shifting.
            if (parent is not JsonObject target) return CanonicalSurfaceApplyStatus.UnsupportedOperation;
            if (hasValue) target[tokens[^1]] = JsonNode.Parse(value.GetRawText());
            else if (!target.Remove(tokens[^1])) return CanonicalSurfaceApplyStatus.MissingPath; // Local missing-delete policy.
        }
        if (!WithinDepth(candidate, 0)) return CanonicalSurfaceApplyStatus.StateLimitExceeded;
        try
        {
            using var output = new BoundedDataStream();
            // Byte accounting is compact UTF8 with JavaScriptEncoder.Default escaping.
            // Preserve JSON number lexemes/types via JsonNode's parsed JsonElement values.
            using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            {
                if (candidate is null) writer.WriteNullValue();
                else candidate.WriteTo(writer);
                writer.Flush();
            }
            using var document = JsonDocument.Parse(output.Written, new JsonDocumentOptions { MaxDepth = MaximumDepth });
            updated = document.RootElement.Clone();
            return CanonicalSurfaceApplyStatus.DataUpdated;
        }
        catch (DataLimitException) { return CanonicalSurfaceApplyStatus.StateLimitExceeded; }
    }

    private static bool WithinDepth(JsonNode? node, int parentDepth)
    {
        if (node is not (JsonObject or JsonArray)) return true;
        int depth = parentDepth + 1;
        if (depth > MaximumDepth) return false;
        return node is JsonObject obj
            ? obj.All(pair => WithinDepth(pair.Value, depth))
            : ((JsonArray)node).All(child => WithinDepth(child, depth));
    }

    private sealed class DataLimitException : Exception { }

    // Abort only the bounded serialization operation. Never construct a full output string
    // before measuring. This bounds stored payload, not snapshots/create bodies/transient RAM.
    private sealed class BoundedDataStream : Stream
    {
        private readonly byte[] _buffer = new byte[MaximumBytes];
        private int _length;
        internal ReadOnlyMemory<byte> Written => _buffer.AsMemory(0, _length);
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > MaximumBytes - _length) throw new DataLimitException();
            buffer.CopyTo(_buffer.AsSpan(_length));
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
