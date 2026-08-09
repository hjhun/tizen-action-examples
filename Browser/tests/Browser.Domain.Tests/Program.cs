using Browser.Domain;

var first = BrowserPage.Create(
    id: "page-first",
    url: "https://www.example.com/first",
    title: "First page",
    details: "Public page summary");
var second = BrowserPage.Create(
    id: "page-second",
    url: "https://www.example.com/second",
    title: "Second page",
    details: "Public page summary");
var catalog = new BrowserPageCatalog([first, second]);

var resolution = catalog.ResolveByIds(["page-second", "page-missing", "page-first", "page-second"]);

if (!resolution.Pages.Select(page => page.Id).SequenceEqual(["page-second", "page-first", "page-second"]))
{
    throw new InvalidOperationException("Browser page resolution must preserve request order and duplicate stable IDs.");
}

if (!resolution.UnresolvedIds.SequenceEqual(["page-missing"]))
{
    throw new InvalidOperationException("Browser page resolution must report unresolved IDs in request order.");
}

Console.WriteLine("PASS: Browser page identity and ordered duplicate-preserving resolution.");
