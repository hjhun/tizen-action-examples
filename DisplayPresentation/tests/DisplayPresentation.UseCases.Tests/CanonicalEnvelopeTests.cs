using System.Text;
using System.Text.Json;
using DisplayPresentation.UseCases;

internal static class CanonicalEnvelopeTests
{
    // Verbatim JSON blocks from the official protocol's four message examples.
    // Inspected 2026-09-08, revision 8ff4651232ab0e02b0123730b502711170637a3a:
    // https://github.com/a2ui-project/a2ui/blob/8ff4651232ab0e02b0123730b502711170637a3a/specification/v0_9_1/docs/a2ui_protocol.md
    // Source SHA256: 0b6a21841b50eb53af85d5ad8c3c0d2922a3a13b4e213f690997d623997bf368
    // Other fixtures below are schema-derived test inputs, not rewritten official examples.
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

    private const string OfficialComponents = """
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

    private const string OfficialData = """
        {
          "version": "v0.9.1",
          "updateDataModel": {
            "surfaceId": "user_profile_card",
            "path": "/user/name",
            "value": "Jane Doe"
          }
        }
        """;

    internal static void Run()
    {
        var checks = RunUnicodeChecks();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        CanonicalEnvelopeReadResult Read(string json) => CanonicalA2UiMessageReader.Read(Encoding.UTF8.GetBytes(json));
        CanonicalA2UiEnvelope Recognize(string json, CanonicalA2UiMessageKind kind)
        {
            var result = Read(json);
            Check(result.Status == CanonicalEnvelopeReadStatus.Recognized && result.Envelope is not null,
                "Expected recognized envelope syntax: " + kind);
            Check(result.Envelope!.Kind == kind && result.Envelope.Version == "v0.9.1", "Kind/version must survive.");
            return result.Envelope;
        }
        void Reject(string json, CanonicalEnvelopeReadStatus status = CanonicalEnvelopeReadStatus.InvalidEnvelope)
        {
            var result = Read(json);
            Check(result.Status == status && result.Envelope is null, "Failure must not return an accepted envelope.");
        }
        string Message(string kind, string body) => "{\"version\":\"v0.9.1\",\"" + kind + "\":" + body + "}";

        var create = Recognize(OfficialCreate, CanonicalA2UiMessageKind.CreateSurface);
        Check(create.Body.GetProperty("catalogId").GetString() ==
            "https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json", "No catalog aliasing.");
        Check(create.Body.GetProperty("theme").GetProperty("primaryColor").GetString() == "#00BFFF" &&
            create.Body.GetProperty("sendDataModel").GetBoolean(), "Preserve catalog-owned values without applying them.");
        Recognize(OfficialDelete, CanonicalA2UiMessageKind.DeleteSurface);
        Recognize(OfficialComponents, CanonicalA2UiMessageKind.UpdateComponents);
        var officialData = Recognize(OfficialData, CanonicalA2UiMessageKind.UpdateDataModel);
        Check(officialData.Body.GetProperty("value").GetString() == "Jane Doe", "Official data message recognized unchanged.");
        var ownedBytes = Encoding.UTF8.GetBytes(OfficialCreate);
        var owned = CanonicalA2UiMessageReader.Read(ownedBytes).Envelope!;
        Array.Fill(ownedBytes, (byte)' ');
        Check(owned.Body.GetProperty("surfaceId").GetString() == "user_profile_card", "Clone must not borrow caller memory.");
        var sendFalse = Recognize(OfficialCreate.Replace("true", "false"), CanonicalA2UiMessageKind.CreateSurface);
        Check(!sendFalse.Body.GetProperty("sendDataModel").GetBoolean(), "Explicit false must survive.");
        var components = Recognize(Message("updateComponents",
            """{"surfaceId":"s","components":[{"id":"root","component":"Text","text":"fixture","futureCatalogField":17}]}"""),
            CanonicalA2UiMessageKind.UpdateComponents);
        Check(components.Body.GetProperty("components")[0].GetProperty("futureCatalogField").GetInt32() == 17,
            "Component internals are preserved, not catalog-validated.");
        var removed = Recognize(Message("updateDataModel", """{"surfaceId":"s"}"""), CanonicalA2UiMessageKind.UpdateDataModel);
        Check(!removed.Body.TryGetProperty("value", out _) && !removed.Body.TryGetProperty("path", out _),
            "Absent value/path must stay absent; omission is not an empty object.");
        foreach (var value in new[] { "null", "true", "42", "1.25", "\"text\"", "{}", "[]", "{\"nested\":[1,null,false]}" })
        {
            var envelope = Recognize(Message("updateDataModel", "{\"surfaceId\":\"s\",\"path\":\" /literal/ \",\"value\":" + value + "}"),
                CanonicalA2UiMessageKind.UpdateDataModel);
            Check(envelope.Body.GetProperty("value").GetRawText() == value &&
                envelope.Body.GetProperty("path").GetString() == " /literal/ ", "Retain explicit value kind and literal path.");
        }
        foreach (var id in new[] { "", " ", "surface / 사진:%2F", "https://EXAMPLE.test/%2f " })
        {
            var literal = Recognize(Message("createSurface", JsonSerializer.Serialize(new { surfaceId = id, catalogId = id })),
                CanonicalA2UiMessageKind.CreateSurface);
            Check(literal.Body.GetProperty("surfaceId").GetString() == id && literal.Body.GetProperty("catalogId").GetString() == id,
                "IDs must not be trimmed, normalized or constrained by the legacy identifier regex.");
        }
        foreach (var version in new[] { "v0.9", "v1.0", "unknown", "" })
            Reject(OfficialDelete.Replace("v0.9.1", version), CanonicalEnvelopeReadStatus.UnsupportedVersion);
        foreach (var json in new[] { "", "{", "null", "[]", "1", "true", "{}", "{}{}", "{\"version\":null}",
            "{\"version\":9}", "{\"version\":true}", "{\"version\":\"v0.9.1\"}",
            """{"version":"v0.9.1","deleteSurface":{"surfaceId":"s"},"createSurface":{"surfaceId":"s","catalogId":"c"}}""",
            """{"version":"v0.9.1","deleteSurface":{"surfaceId":"s"},"extra":0}""",
            """{"version":"v0.9.1","version":"v0.9.1","deleteSurface":{"surfaceId":"s"}}""",
            """{"version":"v0.9.1","deleteSurface":{"surfaceId":"s","surface\u0049d":"other"}}""",
            """{"version":"v0.9.1","updateDataModel":{"surfaceId":"s","value":{"x":1,"x":2}}}""",
            """{"surfaceUpdate":{},"dataModelUpdate":{}}""" }) Reject(json);
        foreach (var kind in new[] { "createSurface", "updateComponents", "updateDataModel", "deleteSurface" })
        {
            foreach (var body in new[] { "null", "[]", "0", "\"text\"", "{}", "{\"surfaceId\":null}", "{\"surfaceId\":5}" })
                Reject(Message(kind, body));
            var validBody = kind switch
            {
                "createSurface" => "{\"surfaceId\":\"s\",\"catalogId\":\"c\"}",
                "updateComponents" => "{\"surfaceId\":\"s\",\"components\":[{}]}",
                _ => "{\"surfaceId\":\"s\"}",
            };
            Reject(Message(kind, validBody[..^1] + ",\"unknown\":true}"));
            Reject(Message(kind, validBody[..^1] + ",\"surfaceId\":\"s\"}"));
        }
        foreach (var body in new[] { """{"surfaceId":"s","catalogId":null}""", """{"surfaceId":"s","catalogId":3}""",
            """{"surfaceId":"s","catalogId":"c","sendDataModel":null}""", """{"surfaceId":"s","catalogId":"c","sendDataModel":"true"}""" })
            Reject(Message("createSurface", body));
        foreach (var value in new[] { "null", "{}", "\"x\"", "[]" })
            Reject(Message("updateComponents", "{\"surfaceId\":\"s\",\"components\":" + value + "}"));
        foreach (var value in new[] { "null", "1", "false", "[]", "{}" })
            Reject(Message("updateDataModel", "{\"surfaceId\":\"s\",\"path\":" + value + "}"));

        Reject("""{"version":"v0.9.1","unknownMessage":{"surfaceId":"s"}}""");
        Reject("""{"deleteSurface":{"surfaceId":"s"}}""");
        Reject("""{"version":"v0.9.1","deleteSurface":{"surfaceId":"s"},}""");
        Reject("""{/*comment*/"version":"v0.9.1","deleteSurface":{"surfaceId":"s"}}""");
        foreach (var value in new[] { "true", "[]", "{}" })
            Reject(Message("deleteSurface", "{\"surfaceId\":" + value + "}"));
        foreach (var value in new[] { "0", "[]", "{}" })
            Reject(Message("createSurface", "{\"surfaceId\":\"s\",\"catalogId\":\"c\",\"sendDataModel\":" + value + "}"));
        var opaqueBytes = Encoding.UTF8.GetBytes(Message("updateDataModel", """{"surfaceId":"s","value":"x"}"""));
        opaqueBytes[Array.LastIndexOf(opaqueBytes, (byte)'x')] = 0xff;
        var opaqueInvalid = CanonicalA2UiMessageReader.Read(opaqueBytes);
        Check(opaqueInvalid.Status == CanonicalEnvelopeReadStatus.InvalidEnvelope && opaqueInvalid.Envelope is null,
            "UTF8 validation includes uninterpreted data values.");

        // Local byte/depth and duplicate-key policies, not universal A2UI restrictions.
        var utf8 = Message("deleteSurface", """{"surfaceId":"사진"}""");
        var atLimit = utf8 + new string(' ', CanonicalA2UiMessageReader.MaximumUtf8Bytes - Encoding.UTF8.GetByteCount(utf8));
        Recognize(atLimit, CanonicalA2UiMessageKind.DeleteSurface);
        Reject(atLimit + " ");
        var invalidUtf8 = CanonicalA2UiMessageReader.Read(new byte[] { 0x7b, 0xff, 0x7d });
        Check(invalidUtf8.Status == CanonicalEnvelopeReadStatus.InvalidEnvelope && invalidUtf8.Envelope is null, "Strict UTF8 input.");
        var nested = new string('[', CanonicalA2UiMessageReader.MaximumDepth - 2) + "null" + new string(']', CanonicalA2UiMessageReader.MaximumDepth - 2);
        Recognize(Message("updateDataModel", "{\"surfaceId\":\"s\",\"value\":" + nested + "}"), CanonicalA2UiMessageKind.UpdateDataModel);
        Reject(Message("updateDataModel", "{\"surfaceId\":\"s\",\"value\":[" + nested + "]}"));
        // The reader has disposed its JsonDocument before returning. Repeated access must remain safe.
        Check(create.Body.GetProperty("surfaceId").GetString() == "user_profile_card" && create.Body.GetRawText().Contains("#00BFFF"),
            "Returned body must own a safe clone.");
        Console.WriteLine($"CanonicalEnvelopeTests: PASS checks={checks} (portable envelope recognition only)");
    }

    private static int RunUnicodeChecks()
    {
        // ASCII JSON escape bytes, never UTF8-encoding an invalid .NET surrogate string.
        // Malformed Unicode anywhere (including opaque data) is a local rejection policy.
        var failures = new List<string>();
        var checks = 0;
        foreach (var escape in new[] { @"\uD800", @"\uDC00", @"\uD800\u0041", @"\uDC00\uD800" })
        {
            var inputs = new Dictionary<string, string>
            {
                ["version"] = "{\"version\":\"" + escape + "\",\"deleteSurface\":{\"surfaceId\":\"s\"}}",
                ["property name"] = "{\"version\":\"v0.9.1\",\"updateDataModel\":{\"surfaceId\":\"s\",\"value\":{\"" + escape + "\":0}}}",
                ["surfaceId"] = "{\"version\":\"v0.9.1\",\"deleteSurface\":{\"surfaceId\":\"" + escape + "\"}}",
                ["catalogId"] = "{\"version\":\"v0.9.1\",\"createSurface\":{\"surfaceId\":\"s\",\"catalogId\":\"" + escape + "\"}}",
                ["opaque value"] = "{\"version\":\"v0.9.1\",\"updateDataModel\":{\"surfaceId\":\"s\",\"value\":[\"" + escape + "\"]}}",
            };
            foreach (var (location, json) in inputs)
            {
                try
                {
                    var result = CanonicalA2UiMessageReader.Read(Encoding.ASCII.GetBytes(json));
                    if (result.Status != CanonicalEnvelopeReadStatus.InvalidEnvelope || result.Envelope is not null)
                        failures.Add(location + " " + escape + ": unsafe result " + result.Status);
                    else checks++;
                }
                catch (InvalidOperationException error)
                {
                    failures.Add(location + " " + escape + ": escaped reader exception " + error);
                }
            }
        }
        const string pair = @"\uD83D\uDE00";
        var valid = """{"version":"v0.9.1","createSurface":{"surfaceId":" PAIR ","catalogId":"PAIR","theme":{"PAIR":"PAIR"}}}""".Replace("PAIR", pair);
        var recognized = CanonicalA2UiMessageReader.Read(Encoding.ASCII.GetBytes(valid));
        if (recognized.Status != CanonicalEnvelopeReadStatus.Recognized || recognized.Envelope is null ||
            recognized.Envelope.Body.GetProperty("surfaceId").GetString() != " 😀 " ||
            recognized.Envelope.Body.GetProperty("catalogId").GetString() != "😀" ||
            recognized.Envelope.Body.GetProperty("theme").GetProperty("😀").GetString() != "😀" ||
            !recognized.Envelope.Body.GetRawText().Contains(pair)) failures.Add("Valid pair/literal not preserved.");
        else checks++;
        var version = CanonicalA2UiMessageReader.Read(Encoding.ASCII.GetBytes("{\"version\":\"" + pair + "\"}"));
        if (version.Status != CanonicalEnvelopeReadStatus.UnsupportedVersion || version.Envelope is not null)
            failures.Add("Valid Unicode in another version must remain UnsupportedVersion.");
        else checks++;
        if (failures.Count != 0) throw new InvalidOperationException("Unicode boundary failures:\n" + string.Join("\n", failures));
        Console.WriteLine($"CanonicalUnicodeChecks: PASS checks={checks} (local Unicode policy)");
        return checks;
    }

}
