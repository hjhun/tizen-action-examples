using System.Text.Json;
using Browser.UseCases;

Assert(BrowserActionContract.TryCreatePage(
    "page-example", "https://example.com/", "Example", "Public page metadata", out var page),
    "Contract must accept bounded public Browser metadata.");
Assert(!BrowserActionContract.TryCreatePage("", "invalid", "", "", out _),
    "Contract must reject malformed Browser entities.");

Assert(BrowserActionContract.HasValidResolverIds(["page-example", "page-missing", "page-example"]),
    "Contract must accept bounded stable resolver IDs.");
Assert(!BrowserActionContract.HasValidResolverIds([" "]),
    "Contract must reject blank resolver IDs.");
Assert(!BrowserActionContract.HasValidResolverIds(Enumerable.Repeat("page", 101).ToArray()),
    "Contract must bound resolver input cardinality.");

var presentation = BrowserActionContract.CreatePresentation(page);
Assert(JsonDocument.Parse(presentation.Template).RootElement.TryGetProperty("surfaceUpdate", out _),
    "Presentation template must be valid surface-update JSON.");
Assert(JsonDocument.Parse(presentation.Document).RootElement.TryGetProperty("dataModelUpdate", out _),
    "Presentation document must be valid data-model-update JSON.");

Console.WriteLine("PASS: Browser provider contract validates bounded input and produces parseable presentation data.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
