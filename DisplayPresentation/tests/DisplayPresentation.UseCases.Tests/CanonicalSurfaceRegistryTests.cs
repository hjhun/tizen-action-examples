using System.Text;
using System.Text.Json;
using DisplayPresentation.UseCases;

internal static class CanonicalSurfaceRegistryTests
{
    // Official protocol create/delete, verbatim blocks, pin 8ff4651232ab0e02b0123730b502711170637a3a.
    // Same source/provenance as CanonicalEnvelopeTests; no version or catalog substitutions.
    private const string OfficialCreate = """
        {
          "version": "v0.9.1",
          "createSurface": {
            "surfaceId": "user_profile_card",
            "catalogId": "https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json",
            "theme": {
              "primaryColor": "#00BFFF"
            },
            "sendDataModel": true
          }
        }
        """;
    private const string OfficialDelete = """
        {
          "version": "v0.9.1",
          "deleteSurface": {
            "surfaceId": "user_profile_card"
          }
        }
        """;

    // Official catalogs/basic/examples/00_simple-text.json first messages element,
    // same pin, whitespace reformatted only. Not a selected-profile positive fixture.
    private const string OfficialV09 = """
        {
          "version": "v0.9",
          "createSurface": {
            "surfaceId": "gallery-simple-text",
            "catalogId": "https://a2ui.org/specification/v0_9/catalogs/basic/catalog.json"
          }
        }
        """;

    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        CanonicalSurfaceApplyStatus Apply(CanonicalSurfaceRegistry registry, string json) =>
            registry.Apply(Encoding.UTF8.GetBytes(json));
        string State(CanonicalSurfaceRegistry registry) => JsonSerializer.Serialize(registry.Snapshot());
        void Reject(CanonicalSurfaceRegistry registry, string json, CanonicalSurfaceApplyStatus expected)
        {
            var before = State(registry);
            Check(Apply(registry, json) == expected, "Expected rejection: " + expected);
            Check(State(registry) == before, "Failure must preserve IDs/version/catalog/body, not just count.");
        }

        var registry = new CanonicalSurfaceRegistry();
        Check(Apply(registry, Create("kept")) == CanonicalSurfaceApplyStatus.Created, "Local schema-derived create.");
        Reject(registry, OfficialCreate, CanonicalSurfaceApplyStatus.UnsupportedCatalog);
        Reject(registry, OfficialV09, CanonicalSurfaceApplyStatus.UnsupportedVersion);
        Reject(registry, "{", CanonicalSurfaceApplyStatus.InvalidEnvelope);
        Reject(registry, "{\"version\":\"v0.9.1\",\"createSurface\":{\"surfaceId\":\"\\uD800\",\"catalogId\":\"x\"}}",
            CanonicalSurfaceApplyStatus.InvalidEnvelope);
        var beforeBytes = State(registry);
        Check(registry.Apply(new byte[] { 0xff }) == CanonicalSurfaceApplyStatus.InvalidEnvelope &&
            State(registry) == beforeBytes, "Invalid UTF8 cannot mutate state.");
        Reject(registry, Create("kept", theme: "changed"), CanonicalSurfaceApplyStatus.DuplicateSurface);
        Reject(registry, Create("kept", catalog: "unknown"), CanonicalSurfaceApplyStatus.UnsupportedCatalog);
        Reject(registry, Create("kept", version: "v0.9", catalog: "unknown"), CanonicalSurfaceApplyStatus.UnsupportedVersion);
        foreach (var catalog in new[] { CanonicalSurfaceRegistry.KnownCatalogId + " ", CanonicalSurfaceRegistry.KnownCatalogId.ToUpperInvariant(), "" })
            Reject(registry, Create("new", catalog: catalog), CanonicalSurfaceApplyStatus.UnsupportedCatalog);
        foreach (var kind in new[] { "updateComponents", "updateDataModel" })
        {
            Reject(registry, Update(kind, "missing"), CanonicalSurfaceApplyStatus.MissingSurface);
            Reject(registry, Update(kind, "kept"), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        }
        Reject(registry, Delete("missing"), CanonicalSurfaceApplyStatus.MissingSurface);
        Check(Apply(registry, Create("user_profile_card")) == CanonicalSurfaceApplyStatus.Created,
            "Explicit LOCAL setup; not an official end-to-end pairing.");
        Check(Apply(registry, OfficialDelete) == CanonicalSurfaceApplyStatus.Deleted,
            "Unmodified official delete succeeds after local setup.");
        Reject(registry, OfficialDelete, CanonicalSurfaceApplyStatus.MissingSurface);
        var old = registry.Snapshot().Single();
        Check(old.Version == "v0.9.1" && old.CatalogId == CanonicalSurfaceRegistry.KnownCatalogId &&
            old.Body.GetProperty("theme").GetProperty("label").GetString() == "original" &&
            old.Body.GetProperty("sendDataModel").GetBoolean(), "Independent version/catalog; opaque body preserved only.");
        Check(Apply(registry, Delete("kept")) == CanonicalSurfaceApplyStatus.Deleted, "Delete known.");
        Check(Apply(registry, Create("kept", theme: "replacement")) == CanonicalSurfaceApplyStatus.Created, "Recreate known.");
        Check(registry.Snapshot().Single().Body.GetProperty("theme").GetProperty("label").GetString() == "replacement" &&
            old.Body.GetProperty("theme").GetProperty("label").GetString() == "original", "New record, old snapshot unaffected.");

        // Returned collection and JSON are detached; callers cannot change registry storage.
        var snapshot = registry.Snapshot();
        var stable = State(registry);
        if (snapshot is IList<CanonicalSurfaceSnapshot> list)
        {
            try { list.Clear(); } catch (NotSupportedException) { }
        }
        using (var bodyCopy = JsonDocument.Parse(snapshot.Count == 0 ? old.Body.GetRawText() : snapshot[0].Body.GetRawText()))
            Check(bodyCopy.RootElement.ValueKind == JsonValueKind.Object, "Caller owns independent JSON document.");
        Check(State(registry) == stable, "Collection edits cannot mutate registry.");
        var callerBody = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(old.Body.GetRawText())!;
        callerBody["surfaceId"] = "caller-edited";
        var callerRecord = old with { SurfaceId = "caller-edited", Body = JsonSerializer.SerializeToElement(callerBody) };
        Check(callerRecord.Body.GetProperty("surfaceId").GetString() == "caller-edited" &&
            State(registry) == stable && old.SurfaceId == "kept", "Caller body/record edits do not reach stored state.");
        var input = Encoding.UTF8.GetBytes(Create("bytes"));
        Check(registry.Apply(input) == CanonicalSurfaceApplyStatus.Created, "Byte input create.");
        Array.Fill(input, (byte)' ');
        Check(registry.Snapshot().Single(x => x.SurfaceId == "bytes").Body.GetProperty("surfaceId").GetString() == "bytes",
            "Stored body survives caller input mutation.");
        foreach (var id in new[] { "", " ", "Kept" })
            Check(Apply(registry, Create(id)) == CanonicalSurfaceApplyStatus.Created, "Literal IDs remain distinct.");
        var other = new CanonicalSurfaceRegistry();
        Apply(other, Create("other"));
        registry.Clear();
        Check(registry.Snapshot().Count == 0 && other.Snapshot().Single().SurfaceId == "other", "Clear owns only this instance.");
        Check(old.Body.GetProperty("surfaceId").GetString() == "kept", "Old snapshot survives Clear.");

        for (int i = 0; i < CanonicalSurfaceRegistry.MaximumSurfaces; i++)
            Check(Apply(registry, Create("s" + i)) == CanonicalSurfaceApplyStatus.Created, "Fill local cap.");
        Reject(registry, Create("overflow"), CanonicalSurfaceApplyStatus.CapacityExceeded);
        Reject(registry, Create("s0"), CanonicalSurfaceApplyStatus.DuplicateSurface);
        Apply(registry, Delete("s0"));
        Check(Apply(registry, Create("after-delete")) == CanonicalSurfaceApplyStatus.Created, "Delete frees capacity.");

        var race = new CanonicalSurfaceRegistry();
        var outcomes = new CanonicalSurfaceApplyStatus[64];
        Parallel.For(0, outcomes.Length, i => outcomes[i] = Apply(race, Create("same", theme: i.ToString())));
        Check(outcomes.Count(x => x == CanonicalSurfaceApplyStatus.Created) == 1 &&
            outcomes.Count(x => x == CanonicalSurfaceApplyStatus.DuplicateSurface) == 63, "One concurrent same-ID winner.");
        var winner = Array.IndexOf(outcomes, CanonicalSurfaceApplyStatus.Created);
        Check(race.Snapshot().Single().Body.GetProperty("theme").GetProperty("label").GetString() == winner.ToString(),
            "Snapshot matches actual winner, not a prescribed scheduling order.");
        race.Clear();
        Parallel.For(0, outcomes.Length, i => outcomes[i] = Apply(race, Create("race" + i)));
        Check(outcomes.Count(x => x == CanonicalSurfaceApplyStatus.Created) == 16 &&
            outcomes.Count(x => x == CanonicalSurfaceApplyStatus.CapacityExceeded) == 48, "Concurrent cap admission is atomic.");
        var raced = race.Snapshot();
        Check(raced.Count == 16 && raced.Select(x => x.SurfaceId).Distinct().Count() == 16 &&
            raced.All(x => outcomes[int.Parse(x.SurfaceId[4..])] == CanonicalSurfaceApplyStatus.Created &&
                x.Version == "v0.9.1" && x.CatalogId == CanonicalSurfaceRegistry.KnownCatalogId &&
                x.Body.GetProperty("surfaceId").GetString() == x.SurfaceId), "Race records match successful admissions.");
        Console.WriteLine($"CanonicalSurfaceRegistryTests: {checks} checks PASS");
    }

    // Local schema-derived positives, deliberately NOT modified official fixtures.
    private static string Create(string id, string theme = "original", string? catalog = null, string version = "v0.9.1") =>
        JsonSerializer.Serialize(new { version, createSurface = new { surfaceId = id,
            catalogId = catalog ?? CanonicalSurfaceRegistry.KnownCatalogId, theme = new { label = theme }, sendDataModel = true } });
    private static string Delete(string id) => JsonSerializer.Serialize(new { version = "v0.9.1", deleteSurface = new { surfaceId = id } });
    private static string Update(string kind, string id) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["version"] = "v0.9.1",
        [kind] = kind == "updateComponents" ? new { surfaceId = id, components = new[] { new { id = "root" } } } : (object)new { surfaceId = id },
    });
}
