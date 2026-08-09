using Browser.Domain;
using Browser.UseCases;

var coordinator = new BrowserNavigationCoordinator(
    new ImmediateRuntime(WebNavigationOutcome.Loaded("Example", "Public page metadata")));

var navigation = await coordinator.NavigateAsync(
    "page-example",
    "https://example.com/",
    CancellationToken.None);

Assert(navigation.Status == BrowserNavigationStatus.Loaded, "Expected the current Browser page to load.");

var query = new BrowserPageQueryService(coordinator);
var resolution = query.ResolveByIds(["page-example", "page-missing", "page-example"]);

Assert(resolution.Pages.Select(page => page.Id).SequenceEqual(["page-example", "page-example"]),
    "Resolver must preserve request order and duplicate IDs.");
Assert(resolution.UnresolvedIds.SequenceEqual(["page-missing"]),
    "Resolver must return missing IDs explicitly.");
Assert(query.GetCurrentPage()?.Id == "page-example", "Current-page lookup must return the shared navigation state.");

await coordinator.DisposeAsync();
Console.WriteLine("PASS: Browser provider query seam exposes current state and ordered duplicate-preserving resolution.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class ImmediateRuntime(WebNavigationOutcome outcome) : IWebRuntime
{
    public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) => Task.FromResult(outcome);
}
