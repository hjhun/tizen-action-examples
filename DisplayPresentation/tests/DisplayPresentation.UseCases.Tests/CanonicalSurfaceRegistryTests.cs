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
        RunDataChecks();
        RunComponentChecks();
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
            // Old components fixture lacks required component/type: now validated, not generic unsupported.
            Reject(registry, Update(kind, "kept"), kind == "updateComponents"
                ? CanonicalSurfaceApplyStatus.InvalidComponent : CanonicalSurfaceApplyStatus.UnsupportedOperation);
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

    private static void RunComponentChecks()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); checks++; }
        var r = new CanonicalSurfaceRegistry();
        CanonicalSurfaceApplyStatus Apply(string json) => r.Apply(Encoding.UTF8.GetBytes(json));
        string State() => JsonSerializer.Serialize(r.Snapshot());
        IReadOnlyList<JsonElement> Nodes(string id = "user_profile_card") => r.Snapshot().Single(x => x.SurfaceId == id).Components;
        void Accept(params string[] nodes) => Check(Apply(ComponentMessage("user_profile_card", nodes)) == CanonicalSurfaceApplyStatus.ComponentsUpdated, "Component admission.");
        void Reject(CanonicalSurfaceApplyStatus status, params string[] nodes)
        {
            var before = State();
            Check(Apply(ComponentMessage("user_profile_card", nodes)) == status, "Expected component result " + status);
            Check(State() == before, "Failure preserves full Body/Data/Components and derived graph.");
        }
        void Reset()
        {
            r.Clear(); Apply(Create("user_profile_card"));
            Apply(DataMessage("user_profile_card", null, "{\"independent\":true}"));
        }
        Reset();
        // Unchanged official protocol updateComponents; LOCAL create is not official pairing.
        const string official = """
        {
          "version": "v0.9.1",
          "updateComponents": {
            "surfaceId": "user_profile_card",
            "components": [
              {
                "id": "root",
                "component": "Column",
                "children": ["user_name", "user_title"]
              },
              {
                "id": "user_name",
                "component": "Text",
                "text": "John Doe"
              },
              {
                "id": "user_title",
                "component": "Text",
                "text": "Software Engineer"
              }
            ]
          }
        }
        """;

        Check(Apply(official) == CanonicalSurfaceApplyStatus.ComponentsUpdated && Nodes().Count == 3, "Official 3 nodes.");
        Accept("{\"text\":\"John Doe\",\"component\":\"Text\",\"id\":\"user_name\"}");
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("user_name", "changed"));
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("user_name", "John Doe", ",\"variant\":\"body\""));
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, ColumnNode("root", "user_title", "user_name"));
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, ColumnNode("user_name"));
        Accept(TextNode("after-failure"));
        Check(Nodes().Count == 4 && r.Snapshot().Single().Data!.Value.GetProperty("independent").GetBoolean(), "Data independence/recovery.");
        foreach (var variant in new[] { "h1", "h2", "h3", "h4", "h5", "caption", "body" })
            Accept(TextNode(variant, "${/literal} **raw**", ",\"variant\":" + JsonSerializer.Serialize(variant)));
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("h1", "${/literal} **raw**")); // explicit optional cannot disappear
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("partial-before-change"), TextNode("user_name", "changed"));
        foreach (var justify in new[] { "start", "center", "end", "spaceBetween", "spaceAround", "spaceEvenly", "stretch" })
            Accept("{\"id\":\"j-" + justify + "\",\"component\":\"Column\",\"children\":[],\"justify\":\"" + justify + "\"}");
        foreach (var align in new[] { "center", "end", "start", "stretch" })
            Accept("{\"id\":\"a-" + align + "\",\"component\":\"Column\",\"children\":[],\"align\":\"" + align + "\"}");
        foreach (var node in new[] { "null", "{}", "{\"id\":\"x\"}", TextNode("x", "x", ",\"variant\":\"bad\""),
            TextNode("x", "x", ",\"unknown\":1"), "{\"id\":\"x\",\"component\":\"Column\",\"children\":[1]}",
            "{\"id\":\"x\",\"component\":\"Text\",\"text\":12}",
            "{\"id\":\"x\",\"component\":\"Column\",\"children\":[],\"align\":\"bad\"}" })
            Reject(CanonicalSurfaceApplyStatus.InvalidComponent, node);
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponent, "{\"id\":\"x\",\"component\":\"UnknownType\"}");
        foreach (var node in new[] { TextNode("x", "x", ",\"weight\":1"), TextNode("x", "x", ",\"accessibility\":{\"label\":\"name\"}"),
            "{\"id\":\"x\",\"component\":\"Text\",\"text\":{\"path\":\"/name\"}}",
            "{\"id\":\"x\",\"component\":\"Text\",\"text\":{\"call\":\"formatString\",\"args\":{},\"returnType\":\"string\"}}",
            "{\"id\":\"x\",\"component\":\"Column\",\"children\":{\"componentId\":\"t\",\"path\":\"/items\"}}" })
            Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentForm, node);
        Reject(CanonicalSurfaceApplyStatus.DuplicateComponentId, TextNode("dup"), TextNode("dup"));
        Reject(CanonicalSurfaceApplyStatus.InvalidComponent, TextNode("partial-add"), "{}");
        Reject(CanonicalSurfaceApplyStatus.InvalidComponent, TextNode("dup"), "{\"id\":\"dup\"}"); // shape before duplicate on each item
        Reject(CanonicalSurfaceApplyStatus.DuplicateComponentId, TextNode("dup"), TextNode("dup"), "{}");
        Reject(CanonicalSurfaceApplyStatus.InvalidComponent, TextNode("user_name", "change"), "{}"); // all shapes before same-ID
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, Enumerable.Repeat("{}", 257).ToArray()); // cardinality before shape
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("user_name", "change"), ColumnNode("cycle", "cycle"));
        Reset();
        Accept(TextNode("orphan"), ColumnNode("before-root", "late", "late"));
        Check(Nodes().Count == 2 && !Nodes().Any(x => x.GetProperty("id").GetString() == "root"), "Buffer without root/placeholder.");
        Accept(ColumnNode("root", "before-root", "other"), ColumnNode("other", "late"));
        Accept(TextNode("late"));
        Check(Nodes().Count == 5 && Nodes().Single(x => x.GetProperty("id").GetString() == "before-root").GetProperty("children").GetArrayLength() == 2,
            "Multiple parents/duplicate edges/unreferenced node retained.");
        Accept(TextNode(""), TextNode(" "), TextNode("Late"));
        Reject(CanonicalSurfaceApplyStatus.ComponentCycle, ColumnNode("disconnected", "disconnected"));
        Accept(ColumnNode("a", "b"));
        Reject(CanonicalSurfaceApplyStatus.ComponentCycle, ColumnNode("b", "a"));
        Accept(TextNode("b"));
        Reset();
        Accept(Enumerable.Range(0,32).Select(i => ColumnNode("n" + i, "n" + (i+1))).ToArray());
        Check(Nodes().Count == 32, "32 known nodes, unresolved target not counted.");
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, TextNode("n32"));
        Reject(CanonicalSurfaceApplyStatus.ComponentCycle, TextNode("n32"), ColumnNode("priority-cycle", "priority-cycle")); // cycle before depth
        Accept(TextNode("unconnected-recovery"));
        // Dense DAG exercises memoized longest path, not exponential recursive enumeration.
        Reset();
        Accept(Enumerable.Range(0,32).Select(i => ColumnNode("d" + i, Enumerable.Range(i+1,31-i).Select(j => "d"+j).ToArray())).ToArray());
        Reset();
        Accept(Enumerable.Range(0,256).Select(i => TextNode("t"+i)).ToArray());
        Accept(TextNode("t0")); // at cap repeat is allowed
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, TextNode("extra"));
        Reject(CanonicalSurfaceApplyStatus.UnsupportedComponentUpdate, TextNode("t0", "changed"), TextNode("extra"));
        Reset();
        Accept(Enumerable.Range(0,4).Select(i => ColumnNode("e"+i, Enumerable.Repeat("missing",256).ToArray())).ToArray());
        Check(Nodes().Count == 4, "1024 references, unresolved remains absent.");
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, ColumnNode("edge-over", "missing"));
        Accept(TextNode("edge-recovery"));
        // Byte bound precedes cycle; edge bound precedes graph as well.
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, ColumnNode("self", "self"));
        Reset();
        for (int i = 0; i < 15; i++) Accept(TextNode("b" + i.ToString("D2"), new string('a',4000)));
        int used = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Nodes()));
        int overhead = Encoding.UTF8.GetByteCount(TextNode("padding", "")) + 1;
        int padding = 65536-used-overhead;
        Accept(TextNode("padding", new string('p',padding)));
        Check(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Nodes())) == 65536, "Exact stored sorted-array byte cap.");
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, TextNode("byte-over"));
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, ColumnNode("byte-cycle", "byte-cycle"));
        Accept(TextNode("b00", new string('a',4000))); // repeat still works, no GC/truncation
        Reset();
        Reject(CanonicalSurfaceApplyStatus.StateLimitExceeded, TextNode("expands", new string('é',12000)).Replace("\\u00E9", "é"));
        Accept(TextNode("after-encoder-failure"));
        var beforeHuge = State();
        Check(Apply(ComponentMessage("user_profile_card", new[] { TextNode("too-large", new string('a',65536)) })) == CanonicalSurfaceApplyStatus.InvalidEnvelope && State() == beforeHuge,
            "C0 envelope bound distinct from cumulative component bytes.");
        var old = r.Snapshot();
        var bytes = Encoding.UTF8.GetBytes(ComponentMessage("user_profile_card", new[] { TextNode("owned") }));
        Check(r.Apply(bytes) == CanonicalSurfaceApplyStatus.ComponentsUpdated, "Owned component body.");
        Array.Fill(bytes,(byte)' ');
        Check(Nodes().Any(x => x.GetProperty("id").GetString() == "owned") && old.Single().Components.Count == 1, "Input lifetime and old snapshot.");
        var view = Nodes();
        if (view is IList<JsonElement> list) { try { list.Clear(); } catch (NotSupportedException) { } }
        Check(Nodes().Count == 2, "Collection cannot alter registry.");
        var editableCopy = System.Text.Json.Nodes.JsonNode.Parse(Nodes()[0].GetRawText())!;
        editableCopy["text"] = "caller-edit";
        Check(Nodes().All(x => x.GetProperty("text").GetString() == "value"), "Caller JSON edits cannot mutate stored component body.");
        Apply(Create("second")); Apply(ComponentMessage("second",new[] { TextNode("second-only") }));
        var otherSession = new CanonicalSurfaceRegistry(); otherSession.Apply(Encoding.UTF8.GetBytes(Create("user_profile_card")));
        var outcomes = new CanonicalSurfaceApplyStatus[32];
        Parallel.For(0,32,i => outcomes[i]=Apply(ComponentMessage("user_profile_card",new[]{TextNode("parallel"+i)})));
        Check(outcomes.All(x=>x==CanonicalSurfaceApplyStatus.ComponentsUpdated) && Nodes().Count == 34 && Nodes("second").Count==1 &&
            otherSession.Snapshot().Single().Components.Count==0, "Concurrent independent additions/session/surface isolation.");
        Check(r.Snapshot().First(x=>x.SurfaceId=="user_profile_card").Data!.Value.GetProperty("independent").GetBoolean(), "Components do not mutate Data.");
        Apply(DataMessage("user_profile_card","/independent","false"));
        Check(Nodes().Count==34, "Data does not mutate Components.");
        Apply(Delete("user_profile_card")); Apply(Create("user_profile_card"));
        Check(Nodes().Count==0 && old.Single().Components.Count==1, "Recreate resets only owned components, snapshots live.");
        r.Clear(); Check(r.Snapshot().Count==0 && otherSession.Snapshot().Count==1, "Clear isolation.");
        Console.WriteLine($"CanonicalComponentChecks: {checks} checks PASS");
    }

    private static string TextNode(string id, string text = "value", string optional = "") =>
        "{\"id\":"+JsonSerializer.Serialize(id)+",\"component\":\"Text\",\"text\":"+JsonSerializer.Serialize(text)+optional+"}";
    private static string ColumnNode(string id, params string[] children) => JsonSerializer.Serialize(new { id, component="Column", children });
    private static string ComponentMessage(string id, string[] nodes) =>
        "{\"version\":\"v0.9.1\",\"updateComponents\":{\"surfaceId\":"+JsonSerializer.Serialize(id)+",\"components\":["+string.Join(',',nodes)+"]}}";

    private static void RunDataChecks()
    {
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); checks++; }
        var r = new CanonicalSurfaceRegistry();
        CanonicalSurfaceApplyStatus Apply(string json) => r.Apply(Encoding.UTF8.GetBytes(json));
        string State() => JsonSerializer.Serialize(r.Snapshot());
        JsonElement Data(string id = "surface_123") => r.Snapshot().Single(x => x.SurfaceId == id).Data!.Value;
        void Set(string? path, string value, string id = "surface_123") =>
            Check(Apply(DataMessage(id, path, value)) == CanonicalSurfaceApplyStatus.DataUpdated, "Set " + path);
        void Reject(string json, CanonicalSurfaceApplyStatus status)
        {
            var before = State();
            Check(Apply(json) == status, "Expected " + status + " for " + json[..Math.Min(100, json.Length)]);
            Check(State() == before, "Failure preserves full Body/Data/identity snapshot.");
        }
        Apply(Create("surface_123"));
        Check(r.Snapshot().Single().Data is null, "Uninitialized is not JSON null.");
        Reject(DataMessage("surface_123", "/x", "1"), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        // Official protocol blocks, same C0 pin; LOCAL create above is not official pairing.
        const string field = """
            {
              "version": "v0.9.1",
              "updateDataModel": {
                "surfaceId": "surface_123",
                "path": "/user/firstName",
                "value": "Alice"
              }
            }
            """;
        const string remove = """
            {
              "version": "v0.9.1",
              "updateDataModel": {
                "surfaceId": "surface_123",
                "path": "/user/tempData"
              }
            }
            """;
        const string whole = """
            {
              "version": "v0.9.1",
              "updateDataModel": {
                "surfaceId": "surface_123",
                "value": {
                  "user": {"firstName": "Alice", "lastName": "Smith"},
                  "preferences": {"theme": "dark"}
                }
              }
            }
            """;
        Check(Apply(whole) == CanonicalSurfaceApplyStatus.DataUpdated, "Official whole replacement.");
        Check(Apply(field) == CanonicalSurfaceApplyStatus.DataUpdated && Data().GetProperty("user").GetProperty("firstName").GetString() == "Alice", "Official field.");
        Set("/user/tempData", "123"); // LOCAL setup for unchanged official remove.
        Check(Apply(remove) == CanonicalSurfaceApplyStatus.DataUpdated && !Data().GetProperty("user").TryGetProperty("tempData", out _), "Official remove.");
        Reject(remove, CanonicalSurfaceApplyStatus.MissingPath);
        foreach (var value in new[] { "null", "true", "false", "1.2300e+40", "\"한글 😀\"", "[1,null,{}]", "{}" })
        {
            Set(null, value);
            using var expected = JsonDocument.Parse(value);
            Check(Data().ValueKind == expected.RootElement.ValueKind, "Root type preserved.");
            if (value == "\"한글 😀\"") Check(Data().GetString() == "한글 😀", "Decoded Unicode literal preserved.");
            if (value == "null") Check(r.Snapshot().Single().Data.HasValue, "Explicit JSON null remains initialized.");
            if (value == "1.2300e+40") Check(Data().GetRawText() == value, "Number lexeme preserved without conversion.");
        }
        Set("/", "{\"parent\":{},\"array\":[1,null],\"scalar\":1,\"nil\":null}");
        foreach (var key in new[] { "01", "-", "0", "", "a~1b", "m~0n", "~01", "a%2Fb", " " }) Set("/parent/" + key, "null");
        var parent = Data().GetProperty("parent");
        Check(parent.TryGetProperty("", out _) && parent.TryGetProperty("a/b", out _) && parent.TryGetProperty("m~n", out _) &&
            parent.TryGetProperty("~1", out _) && parent.TryGetProperty("a%2Fb", out _) && parent.TryGetProperty(" ", out _), "Single decoding, empty key, no trim/percent decode.");
        foreach (var value in new[] { "1.2300e+40", "true", "\"text\"", "[1,2]", "{\"nested\":null}" }) Set("/parent/leaf", value);
        Set("/parent/nullLeaf", "null");
        Check(Data().GetProperty("parent").GetProperty("nullLeaf").ValueKind == JsonValueKind.Null, "Leaf null is present.");
        Check(Apply(DataMessage("surface_123", "/parent/nullLeaf", null)) == CanonicalSurfaceApplyStatus.DataUpdated &&
            !Data().GetProperty("parent").TryGetProperty("nullLeaf", out _), "Missing value removes, not null assignment.");
        Set("/parent", "{}");
        Check(!Data().GetProperty("parent").EnumerateObject().Any(), "Replacement not merge.");
        foreach (var path in new[] { "", "/array/0", "/array/01", "/array/-", "/array/9000", "/missing/x", "/scalar/x", "/nil/x" })
            Reject(DataMessage("surface_123", path, "1"), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        Reject(DataMessage("surface_123", "/array/0", null), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        foreach (var path in new[] { "x", "#/x", "/~", "/~2", "/array/~x" })
            Reject(DataMessage("surface_123", path, "1"), CanonicalSurfaceApplyStatus.InvalidPath);
        Reject(DataMessage("surface_123", null, null), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        Reject(DataMessage("surface_123", "/", null), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        Reject(DataMessage("missing", "/~x", "1"), CanonicalSurfaceApplyStatus.MissingSurface);
        Reject(DataMessage("surface_123", "/" + new string('x', 1024), "1"), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Set("/" + new string('x', 1023), "1"); // 1024 UTF8 bytes including slash.
        Set("/" + new string('é', 511) + "x", "1");
        Reject(DataMessage("surface_123", "/" + new string('é', 512), "1"), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Reject(DataMessage("surface_123", new string('/', 33), "1"), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Reject(DataMessage("surface_123", new string('/', 32), "1"), CanonicalSurfaceApplyStatus.UnsupportedOperation);
        Reject(DataMessage("surface_123", "/~" + new string('x', 1024), "1"), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Reject("{", CanonicalSurfaceApplyStatus.InvalidEnvelope);
        Reject("{\"version\":\"v0.9.1\",\"updateDataModel\":{\"surfaceId\":\"surface_123\",\"value\":\"\\uD800\"}}", CanonicalSurfaceApplyStatus.InvalidEnvelope);

        // Compact default-encoder measurement; cumulative small updates reach exact 64KiB.
        Set(null, "{}");
        for (int i = 0; i < 16; i++) Set("/k" + i.ToString("D2"), JsonSerializer.Serialize(new string('a', 4000)));
        int used = Encoding.UTF8.GetByteCount(Data().GetRawText());
        int pad = 65536 - used - 9; // ,"pad":"" adds nine bytes.
        Set("/pad", JsonSerializer.Serialize(new string('p', pad)));
        Check(Encoding.UTF8.GetByteCount(Data().GetRawText()) == 65536, "Exact cumulative byte boundary.");
        Reject(DataMessage("surface_123", "/pad", JsonSerializer.Serialize(new string('p', pad + 1))), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Set("/pad", "\"\"");
        Set("/recovery", "true");
        Set(null, "{}");
        // Non-ASCII JSON input remains small; default encoder expands each é to six bytes.
        Reject(DataMessage("surface_123", "/text", "\"" + new string('é', 12000) + "\""), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Set("/ok", "1");
        Set(null, "{}");
        for (int depth = 1; depth <= 31; depth++) Set("/" + string.Join('/', Enumerable.Repeat("x", depth)), "{}");
        Check(Data().ValueKind == JsonValueKind.Object, "Data depth32 reached by small upserts beyond message nesting.");
        Reject(DataMessage("surface_123", "/" + string.Join('/', Enumerable.Repeat("x", 32)), "{}"), CanonicalSurfaceApplyStatus.StateLimitExceeded);
        Set("/" + string.Join('/', Enumerable.Repeat("x", 32)), "1"); // Exact token32 at existing parent depth32.
        Set("/recovered", "1");

        Reject(DataMessage("surface_123", "/large", "\"" + new string('a', 65536) + "\""), CanonicalSurfaceApplyStatus.InvalidEnvelope);
        var old = r.Snapshot();
        var bytes = Encoding.UTF8.GetBytes(DataMessage("surface_123", null, "{\"fresh\":1}"));
        Check(r.Apply(bytes) == CanonicalSurfaceApplyStatus.DataUpdated, "Owned input.");
        Array.Fill(bytes, (byte)' ');
        Check(Data().GetProperty("fresh").GetInt32() == 1 && !old.Single().Data!.Value.TryGetProperty("fresh", out _), "Snapshot/input ownership.");
        Apply(Create("second")); Set(null, "123", "second");
        var second = r.Snapshot().Single(x => x.SurfaceId == "second");
        var other = new CanonicalSurfaceRegistry(); other.Apply(Encoding.UTF8.GetBytes(Create("surface_123")));
        Apply(Delete("surface_123")); Apply(Create("surface_123"));
        Check(r.Snapshot().Single(x => x.SurfaceId == "surface_123").Data is null &&
            second.Data!.Value.GetInt32() == 123 && other.Snapshot().Single().Data is null, "Surface/session isolation and recreation reset.");
        for (int i = 0; i < 14; i++) Apply(Create("cap" + i));
        Reject(Create("overflow-data-session"), CanonicalSurfaceApplyStatus.CapacityExceeded);
        Set(null, "{}"); // Data updates remain usable at the surface-count cap.
        var statuses = new CanonicalSurfaceApplyStatus[32];
        Parallel.For(0, 32, i => statuses[i] = Apply(DataMessage("surface_123", "/key" + i, i.ToString())));
        Check(statuses.All(x => x == CanonicalSurfaceApplyStatus.DataUpdated) && Data().EnumerateObject().Count() == 32 &&
            Enumerable.Range(0,32).All(i => Data().GetProperty("key" + i).GetInt32() == i), "Separate-key updates lose none.");
        Parallel.For(0, 32, i => statuses[i] = Apply(DataMessage("surface_123", "/winner", "{\"a\":" + i + ",\"b\":" + i + "}")));
        var win = Data().GetProperty("winner");
        Check(statuses.All(x => x == CanonicalSurfaceApplyStatus.DataUpdated) && win.GetProperty("a").GetInt32() == win.GetProperty("b").GetInt32(), "Same-key atomic winner, unspecified order.");
        CanonicalSurfaceApplyStatus deletion = default, creation = default, update = default;
        Parallel.Invoke(() => { deletion = Apply(Delete("surface_123")); creation = Apply(Create("surface_123")); },
            () => update = Apply(DataMessage("surface_123", null, "42")));
        var final = r.Snapshot().Single(x => x.SurfaceId == "surface_123").Data;
        Check(deletion == CanonicalSurfaceApplyStatus.Deleted && creation == CanonicalSurfaceApplyStatus.Created &&
            ((update == CanonicalSurfaceApplyStatus.MissingSurface && final is null) ||
             (update == CanonicalSurfaceApplyStatus.DataUpdated && (final is null || final.Value.GetInt32() == 42))),
            "Only possible lock orders; no wire generation protection.");
        r.Clear(); Check(r.Snapshot().Count == 0 && other.Snapshot().Count == 1 && second.Data!.Value.GetInt32() == 123, "Data Clear ownership.");
        Console.WriteLine($"CanonicalDataModelChecks: {checks} checks PASS");
    }

    // null value argument means omitted; the string "null" means explicit JSON null.
    private static string DataMessage(string id, string? path, string? value) =>
        "{\"version\":\"v0.9.1\",\"updateDataModel\":{\"surfaceId\":" + JsonSerializer.Serialize(id) +
        (path is null ? "" : ",\"path\":" + JsonSerializer.Serialize(path)) +
        (value is null ? "" : ",\"value\":" + value) + "}}";

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
