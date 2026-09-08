using System.Text.Json;

namespace DisplayPresentation.UseCases;

public enum CanonicalSurfaceApplyStatus
{
    Created, Deleted, InvalidEnvelope, UnsupportedVersion, UnsupportedCatalog,
    DuplicateSurface, MissingSurface, CapacityExceeded, UnsupportedOperation,
}

/// <summary>Immutable registry metadata and opaque create body, not a rendered surface.</summary>
public sealed record CanonicalSurfaceSnapshot(string SurfaceId, string Version, string CatalogId, JsonElement Body);

/// <summary>
/// Session-owned, portable create/delete registry. Not connected to legacy rendering or providers.
/// Catalog admission identifies one pre-registered literal; it does not validate catalog semantics.
/// Theme/sendDataModel are preserved only. No fetching, aliases, or external catalog registration.
/// </summary>
public sealed class CanonicalSurfaceRegistry
{
    // Local resource policy, not an upstream protocol limit.
    public const int MaximumSurfaces = 16;
    public const string KnownCatalogId = "https://a2ui.org/specification/v0_9/catalogs/basic/catalog.json";
    // Provenance of the reviewed definition, NOT a hash established by an incoming catalogId.
    // Pin 8ff4651232ab0e02b0123730b502711170637a3a, inspected 2026-09-08:
    // specification/v0_9_1/catalogs/basic/catalog.json
    // SHA256 8cc94d0a482e67048f9fc989964ca5da56fe42f531d919315a508989fb22e13e
    private readonly object _sync = new();
    private readonly Dictionary<string, CanonicalSurfaceSnapshot> _surfaces = new(StringComparer.Ordinal);

    public CanonicalSurfaceApplyStatus Apply(ReadOnlyMemory<byte> utf8)
    {
        // C0 input/resource checks precede catalog admission and any state access.
        var read = CanonicalA2UiMessageReader.Read(utf8);
        if (read.Status == CanonicalEnvelopeReadStatus.InvalidEnvelope)
            return CanonicalSurfaceApplyStatus.InvalidEnvelope;
        if (read.Status == CanonicalEnvelopeReadStatus.UnsupportedVersion)
            return CanonicalSurfaceApplyStatus.UnsupportedVersion;
        var envelope = read.Envelope!;
        var body = envelope.Body;
        var id = body.GetProperty("surfaceId").GetString()!;
        string? catalog = null;
        if (envelope.Kind == CanonicalA2UiMessageKind.CreateSurface)
        {
            catalog = body.GetProperty("catalogId").GetString()!;
            if (!string.Equals(catalog, KnownCatalogId, StringComparison.Ordinal))
                return CanonicalSurfaceApplyStatus.UnsupportedCatalog;
        }

        lock (_sync)
        {
            if (envelope.Kind == CanonicalA2UiMessageKind.CreateSurface)
            {
                if (_surfaces.ContainsKey(id)) return CanonicalSurfaceApplyStatus.DuplicateSurface;
                if (_surfaces.Count >= MaximumSurfaces) return CanonicalSurfaceApplyStatus.CapacityExceeded;
                _surfaces.Add(id, new(id, envelope.Version, catalog!, body));
                return CanonicalSurfaceApplyStatus.Created;
            }
            if (!_surfaces.ContainsKey(id)) return CanonicalSurfaceApplyStatus.MissingSurface;
            if (envelope.Kind == CanonicalA2UiMessageKind.DeleteSurface)
            {
                _surfaces.Remove(id);
                return CanonicalSurfaceApplyStatus.Deleted;
            }
            // Component/data updates are deliberately not applied, including to existing surfaces.
            return CanonicalSurfaceApplyStatus.UnsupportedOperation;
        }
    }

    public IReadOnlyList<CanonicalSurfaceSnapshot> Snapshot()
    {
        lock (_sync)
        {
            // Detached read-only collection, immutable records, cloned JSON ownership.
            return Array.AsReadOnly(_surfaces.Values.OrderBy(x => x.SurfaceId, StringComparer.Ordinal)
                .Select(x => x with { Body = x.Body.Clone() }).ToArray());
        }
    }

    /// <summary>The owning session may clear only this instance; no global/platform resources.</summary>
    public void Clear()
    {
        lock (_sync) _surfaces.Clear();
    }
}
