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

var sanitized = BrowserPage.Create("public-page", "https://example.com/path?query=private#fragment", "Public", "Summary");
if (sanitized.Url != "https://example.com/path")
{
    throw new InvalidOperationException("Browser page metadata must remove query and fragment at the domain boundary.");
}

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

var workspace = BrowserTabWorkspace.Create("tab-1").OpenTabs();
for (var index = 0; index < 19; index++)
{
    if (!workspace.TryCreateTab(out workspace, out var createdTabId) ||
        workspace.SelectedTabId != createdTabId ||
        workspace.Surface != BrowserWorkspaceSurface.Tabs)
    {
        throw new InvalidOperationException("A new normal tab must receive a stable ID, remain selected, and keep the Tabs surface open.");
    }
}

if (workspace.Tabs.Count != BrowserTabWorkspace.MaximumTabs ||
    workspace.Tabs.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != BrowserTabWorkspace.MaximumTabs ||
    workspace.CanCreateTab ||
    workspace.TryCreateTab(out _, out _))
{
    throw new InvalidOperationException("The normal-mode workspace must enforce exactly 20 unique stable tabs.");
}

var closeWorkspace = BrowserTabWorkspace.Create("tab-1").OpenTabs();
closeWorkspace.TryCreateTab(out closeWorkspace, out var secondTabId);
closeWorkspace.TryCreateTab(out closeWorkspace, out var thirdTabId);
closeWorkspace = closeWorkspace.SelectTab(secondTabId).OpenTabs();
if (!closeWorkspace.TryRequestClose(secondTabId, out var confirmation) ||
    confirmation.Surface != BrowserWorkspaceSurface.CloseConfirmation ||
    confirmation.PendingCloseTabId != secondTabId ||
    confirmation.PreferredFocus != BrowserWorkspaceFocus.CancelClose)
{
    throw new InvalidOperationException("Close request must open one modal with Cancel as initial focus.");
}

var cancelled = confirmation.CancelClose();
if (!cancelled.Tabs.Select(tab => tab.Id).SequenceEqual(["tab-1", secondTabId, thirdTabId]) ||
    cancelled.SelectedTabId != secondTabId ||
    cancelled.PreferredFocusTabId != secondTabId)
{
    throw new InvalidOperationException("Cancel/Back must preserve tab state and restore the invoking tab focus.");
}

var confirmed = confirmation.ConfirmClose();
if (!confirmed.Tabs.Select(tab => tab.Id).SequenceEqual(["tab-1", thirdTabId]) ||
    confirmed.SelectedTabId != thirdTabId ||
    confirmed.PreferredFocusTabId != thirdTabId ||
    confirmed.Surface != BrowserWorkspaceSurface.Tabs)
{
    throw new InvalidOperationException("Confirm must remove exactly one tab and select the nearest remaining tab.");
}

if (!confirmed.TryCreateTab(out _, out var replacementTabId) || replacementTabId == secondTabId)
{
    throw new InvalidOperationException("A newly created tab must not reuse a closed tab's stable ID.");
}

if (BrowserTabWorkspace.Create("only-tab").OpenTabs().TryRequestClose("only-tab", out _))
{
    throw new InvalidOperationException("The last normal tab cannot be closed.");
}

var longTitlePage = BrowserPage.Create("long-tab", "https://example.com/long", new string('T', 120), "Public");
var longTitleWorkspace = BrowserTabWorkspace.Restore(
    [BrowserTab.Create("tab-1"), BrowserTab.Create("long-tab", longTitlePage)],
    "long-tab").OpenTabs();
longTitleWorkspace.TryRequestClose("long-tab", out var longTitleConfirmation);
if (longTitleConfirmation.PendingCloseTitle.Length != 80)
{
    throw new InvalidOperationException("Close confirmation must truncate a page title to 80 characters.");
}

var untitledPage = BrowserPage.Create("untitled", "https://example.com/untitled", string.Empty, "Public");
var untitledWorkspace = BrowserTabWorkspace.Restore(
    [BrowserTab.Create("tab-1"), BrowserTab.Create("untitled", untitledPage)],
    "untitled").OpenTabs();
untitledWorkspace.TryRequestClose("untitled", out var untitledConfirmation);
if (untitledConfirmation.PendingCloseTitle != "New tab")
{
    throw new InvalidOperationException("A blank page title must use the bounded New tab confirmation label.");
}

Console.WriteLine("PASS: Browser tabs enforce stable IDs, max-20, selection, close confirmation, nearest selection, and focus restoration.");
