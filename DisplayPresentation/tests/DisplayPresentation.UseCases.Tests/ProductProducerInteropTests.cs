using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Browser.Domain;
using Browser.UseCases;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;
using PhotoGallery.Domain;
using PhotoGallery.UseCases;

internal static class ProductProducerInteropTests
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        var parser = new A2UiPresentationParser();
        void CheckPages(PresentationInput input, string[] expected, int pageCount)
        {
            var outcome = parser.Parse(input);
            Check(outcome.IsSuccess, $"Actual producer output must parse: {outcome.Failure}");
            var surface = outcome.Plan!.Surface;
            Check(ProducerInteropTests.Texts(surface.Root).Select(x => x.Value).SequenceEqual(expected),
                "Producer field order and bounded values must survive parsing.");
            var pages = PresentationPages.Create(surface);
            Check(pages.Count == pageCount, "All producer fields must remain reachable through paging.");
            Check(pages.SelectMany(x => ProducerInteropTests.Texts(x.Root))
                .SequenceEqual(ProducerInteropTests.Texts(surface.Root)), "Paging must not lose or duplicate fields.");
            foreach (var page in pages)
            {
                var texts = ProducerInteropTests.Texts(page.Root).ToArray();
                var snapshot = A2UiPresentationSerializer.Serialize(page);
                var restored = parser.Parse(snapshot);
                Check(restored.IsSuccess && ProducerInteropTests.Texts(restored.Plan!.Surface.Root).SequenceEqual(texts),
                    "The same semantic IDs, roles and values must survive page serialization.");
                using var document = JsonDocument.Parse(snapshot.Document);
                var values = document.RootElement.GetProperty("dataModelUpdate").GetProperty("value");
                Check(values.EnumerateObject().Select(x => x.Name).Order()
                    .SequenceEqual(texts.Select(x => x.Id).Order()), "Snapshot data must contain only this page's fields.");
            }
        }

        var browser = BrowserPage.Create("browser-fixture", "https://example.test/page?token=private-query#private-fragment",
            "Browser fixture", "Public details");
        var profiles = BrowserActionContract.CreatePresentations(browser);
        var legacy = profiles.LegacyDisplayCompatibility;
        CheckPages(new(legacy.Template, legacy.Document),
            ["Browser page", "Browser fixture", "https://example.test/page", "Public details"], 1);
        Check(!legacy.Document.Contains("private-query") && !legacy.Document.Contains("private-fragment"),
            "Browser public output must not expose URL query or fragment.");
        var longBrowser = BrowserActionContract.CreateLegacyDisplayPresentation(
            BrowserPage.Create("long-browser", "https://example.test/", new string('t', 512), new string('d', 2048)));
        CheckPages(new(longBrowser.Template, longBrowser.Document),
            ["Browser page", new string('t', 256), "https://example.test/", new string('d', 256)], 1);
        Check(!parser.Parse(new(profiles.Canonical.Messages[0], profiles.Canonical.Messages[2])).IsSuccess,
            "Legacy parser acceptance must not imply support for canonical lifecycle messages.");

        // Unmodified production create only: envelope recognition is not catalog admission.
        var createBytes = Encoding.UTF8.GetBytes(profiles.Canonical.Messages[0]);
        var recognized = CanonicalA2UiMessageReader.Read(createBytes);
        Check(recognized.Status == CanonicalEnvelopeReadStatus.Recognized &&
            recognized.Envelope?.Kind == CanonicalA2UiMessageKind.CreateSurface,
            "Browser create must be recognized by C0 independently of catalog support.");
        var registry = new CanonicalSurfaceRegistry();
        // LOCAL setup, not a rewritten producer or official success pairing.
        var localCreate = JsonSerializer.Serialize(new { version = "v0.9.1", createSurface = new {
            surfaceId = "kept", catalogId = CanonicalSurfaceRegistry.KnownCatalogId,
            theme = new { label = "original" }, sendDataModel = true } });
        Check(registry.Apply(Encoding.UTF8.GetBytes(localCreate)) == CanonicalSurfaceApplyStatus.Created,
            "Local admitted surface must exist before the producer rejection.");
        Check(registry.Apply(Encoding.UTF8.GetBytes("""
            {"version":"v0.9.1","updateDataModel":{"surfaceId":"kept","value":{"saved":true}}}
            """)) == CanonicalSurfaceApplyStatus.DataUpdated, "Local data setup must apply.");
        Check(registry.Apply(Encoding.UTF8.GetBytes("""
            {"version":"v0.9.1","updateComponents":{"surfaceId":"kept","components":[{"id":"root","component":"Text","text":"Kept literal"}]}}
            """)) == CanonicalSurfaceApplyStatus.ComponentsUpdated, "Local component setup must apply.");
        var beforeCreate = JsonSerializer.Serialize(registry.Snapshot());
        Check(registry.Apply(createBytes) == CanonicalSurfaceApplyStatus.UnsupportedCatalog,
            "Actual Browser create must fail exact catalog admission, not version recognition.");
        Check(JsonSerializer.Serialize(registry.Snapshot()) == beforeCreate,
            "Rejected producer create must preserve all IDs, versions, catalogs, bodies, data and components.");
        // Do not apply the producer's subsequent component/data messages after failed create.

        foreach (var owned in new[] { false, true })
        foreach (var favorite in new[] { false, true })
        {
            var photo = PhotoRecord.Create("photo-fixture", "Photo fixture", new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero),
                "private-location", "/private/photo-fixture.png", "private-note")
                with { Album = "Fixture album", Owned = owned, Favorite = favorite };
            var output = PhotoPresentation.Create(photo);
            CheckPages(new(output.Template, output.Document),
                ["Photo fixture", "2026-09-08", "Fixture album", favorite ? "Favorite" : "Not a favorite",
                    owned ? "Imported copy" : "Device library photo"], 2);
            Check(!output.Document.Contains("private-location") && !output.Document.Contains("/private/") &&
                !output.Document.Contains("private-note"), "Photo presentation must not expose private file metadata.");

            var changed = JsonNode.Parse(output.Document)!;
            changed["dataModelUpdate"]!["value"]!["title"] = 42;
            var rejected = parser.Parse(new(output.Template, changed.ToJsonString()));
            Check(!rejected.IsSuccess && rejected.Failure?.Kind == RenderFailureKind.InvalidInput,
                "Wrong-type bound values in a real producer document must be rejected.");
        }

        Console.WriteLine($"ProductProducerInteropTests: PASS checks={checks} (production builders: legacy host and Browser canonical admission mismatch only)");
    }
}
