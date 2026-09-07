using System.Text.Json;
using Browser.Domain;
using Browser.UseCases;
using Browser.ActionProvider;
using Browser.ViewActionProvider;
using RPCPort.TizenActionBrowser;


if (args is ["--providers"])
{
    CheckProviders();
    Console.WriteLine("PASS: actual provider return-value checks (no Parcel runtime claim).");
    return;
}
var assertionCount = 0;
void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    assertionCount++;
}

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
// Legacy renderer semantic-tree acceptance is verified against the installed renderer,
// independently of these producer/provider tests; no renderer project is built here.

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

Console.WriteLine($"PORTABLE COMPLETE: {assertionCount} assertions");

static void CheckProviders()
{
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
var queries = new BrowserPageQueryService(new BrowserAgentStateRegistry());
var provider = new BrowserActionService(queries);
Assert(!provider.GetCurrentPage(out var emptyPage).Success && emptyPage.Url == "" && emptyPage.Title == "", "Hidden page failure initializes WebPageInfo.");
Assert(!provider.GetTabs(out var tabs).Success && tabs.Count == 0, "Unconfigured tab snapshot fails with an empty list.");
Assert(!provider.ToPresentation(new TizenEntityWebPageInfo(), out var emptyPresentation).Success && emptyPresentation.Template == "" && emptyPresentation.Document == "", "Invalid Presentation initializes both serializer strings.");
Assert(!provider.ControlTab(new TizenEntityTabCommand(), out var emptyTab).Success && emptyTab.Page.Url == "", "Unsupported tab initializes nested page.");
Assert(!provider.ToCalendar(new TizenEntityWebPageInfo(), out var emptyEvent).Success && emptyEvent.StartDate == "", "Unsupported Calendar initializes Event.");
Assert(!provider.Search(new TizenEntityBrowserQuery(), out var emptySearch).Success && emptySearch.Answer.Count == 0 && emptySearch.Place.Count == 0 && emptySearch.Product.Count == 0 && emptySearch.Person.Count == 0, "Unsupported search initializes all nested lists.");
var viewProvider = new BrowserViewActionService();
Assert(!viewProvider.FindById("", out var emptyView).Success && emptyView.Annotation.EntityInfo == "" && emptyView.ScreenBounds is not null && emptyView.WindowBounds is not null, "View failure initializes nested annotation and bounds.");
Assert(viewProvider.GetAnnotatedViews(out var noViews).Success && noViews.Count == 0, "No rendered views is a successful empty query, not a failure.");

}
