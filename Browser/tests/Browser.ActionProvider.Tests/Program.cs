using System.Text.Json;
using Browser.Domain;
using Browser.UseCases;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;

Assert(BrowserActionContract.TryCreatePage(
    "page-example", "https://example.com/", "Example", "Public page metadata", out var page),
    "Contract must accept bounded public Browser metadata.");
Assert(!BrowserActionContract.TryCreatePage("", "invalid", "", "", out _),
    "Contract must reject malformed Browser entities.");

Assert(BrowserActionContract.HasValidResolverIds(["page-example", "page-missing", "page-example"]),
    "Contract must accept bounded stable resolver IDs.");
Assert(BrowserActionContract.HasValidResolverIds(Enumerable.Range(0, 50).Select(index => $"page-{index}").ToArray()),
    "Resolver input must accept the exact 50-ID boundary.");
Assert(!BrowserActionContract.HasValidResolverIds([]),
    "Resolver input must contain at least one stable ID.");
Assert(!BrowserActionContract.HasValidResolverIds([" "]),
    "Contract must reject blank resolver IDs.");
Assert(!BrowserActionContract.HasValidResolverIds(Enumerable.Repeat("page", 51).ToArray()),
    "Resolver input must honor the public 1-to-50 cardinality contract.");

var presentations = BrowserActionContract.CreatePresentations(page);
if (args is ["--emit-canonical", var messageIndex] &&
    int.TryParse(messageIndex, out var parsedIndex) && parsedIndex is >= 0 and <= 3)
{
    Console.WriteLine(parsedIndex == 3
        ? presentations.Canonical.DeleteMessage
        : presentations.Canonical.Messages[parsedIndex]);
    return;
}
Assert(presentations.Canonical.Version == "v0.9.1" &&
       presentations.Canonical.MimeType == "application/a2ui+json" &&
       presentations.Canonical.CatalogId == BrowserActionContract.BasicCatalogId &&
       presentations.Canonical.Messages.Count == 3,
    "Canonical Browser output must declare the official current A2UI version, MIME type, catalog, and ordered initial lifecycle.");

using var createMessage = JsonDocument.Parse(presentations.Canonical.Messages[0]);
using var componentsMessage = JsonDocument.Parse(presentations.Canonical.Messages[1]);
using var dataMessage = JsonDocument.Parse(presentations.Canonical.Messages[2]);
Assert(createMessage.RootElement.GetProperty("version").GetString() == "v0.9.1" &&
       createMessage.RootElement.GetProperty("createSurface").GetProperty("catalogId").GetString() == BrowserActionContract.BasicCatalogId,
    "Canonical A2UI must begin with createSurface and the negotiated Basic Catalog identifier.");
Assert(componentsMessage.RootElement.GetProperty("version").GetString() == "v0.9.1" &&
       componentsMessage.RootElement.GetProperty("updateComponents").GetProperty("components")[0].GetProperty("id").GetString() == "root" &&
       componentsMessage.RootElement.GetProperty("updateComponents").GetProperty("components")[0].GetProperty("component").GetString() == "Column",
    "Canonical A2UI must use the v0.9.1 flat component model with a root component.");
Assert(dataMessage.RootElement.GetProperty("version").GetString() == "v0.9.1" &&
       dataMessage.RootElement.GetProperty("updateDataModel").GetProperty("path").GetString() == "/" &&
       dataMessage.RootElement.GetProperty("updateDataModel").GetProperty("value").GetProperty("browser").GetProperty("id").GetString() == page.Id,
    "Canonical A2UI data must come from the same bounded Browser snapshot.");
Assert(JsonDocument.Parse(presentations.Canonical.DeleteMessage).RootElement.TryGetProperty("deleteSurface", out _),
    "Canonical A2UI must expose a version-correct deleteSurface lifecycle message.");

var legacy = presentations.LegacyDisplayCompatibility;
using var legacyTemplate = JsonDocument.Parse(legacy.Template);
using var legacyDocument = JsonDocument.Parse(legacy.Document);
var legacyComponents = legacyTemplate.RootElement.GetProperty("surfaceUpdate").GetProperty("components");
Assert(legacyComponents.GetArrayLength() == 5 &&
       legacyComponents[0].GetProperty("component").TryGetProperty("Column", out _) &&
       legacyComponents[1].GetProperty("component").TryGetProperty("Text", out _),
    "The named legacy DisplayPresentation adapter must emit its supported Column/Text semantic tree rather than an empty fixture.");
Assert(legacyDocument.RootElement.GetProperty("dataModelUpdate").GetProperty("path").GetString() == "/" &&
       legacyDocument.RootElement.GetProperty("dataModelUpdate").GetProperty("value").GetProperty("title").GetString() == page.Title,
    "Legacy compatibility data must use the same bounded Browser snapshot.");
var displayOutcome = new A2UiPresentationParser().Parse(new PresentationInput(legacy.Template, legacy.Document));
Assert(displayOutcome is
{
    IsSuccess: true,
    Plan.Surface.Root: VerticalGroup
    {
        Children:
        [
            TextValue { Value: "Browser page" },
            TextValue { Value: "Example" },
            TextValue { Value: "https://example.com/" },
            TextValue { Value: "Public page metadata" },
        ],
    },
},
    "The actual current DisplayPresentation compatibility parser must accept and preserve the Browser semantic tree.");

var privatePage = BrowserPage.Create(
    "page-private",
    "https://example.com/private?token=secret#fragment",
    new string('T', 300),
    new string('D', 400));
var privatePresentations = BrowserActionContract.CreatePresentations(privatePage);
var allPayload = string.Join("\n", privatePresentations.Canonical.Messages) + privatePresentations.LegacyDisplayCompatibility.Template + privatePresentations.LegacyDisplayCompatibility.Document;
Assert(!allPayload.Contains("token=secret", StringComparison.Ordinal) &&
       !allPayload.Contains("fragment", StringComparison.Ordinal) &&
       !allPayload.Contains(new string('T', 257), StringComparison.Ordinal) &&
       !allPayload.Contains(new string('D', 257), StringComparison.Ordinal) &&
       allPayload.Length < BrowserActionContract.MaximumPresentationCharacters,
    "Both A2UI profiles must redact private URL parts and bound every displayed value and total payload.");

Console.WriteLine("PASS: Browser provider contract validates entities and produces bounded canonical plus named legacy A2UI profiles.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
