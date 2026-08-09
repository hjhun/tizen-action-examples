using System.Text.Json;
using Browser.Domain;
using Browser.Persistence;

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

var snapshot = BrowserSessionSnapshot.CreateTabs(
    "page-second",
    [BrowserTab.Create("page-first", first), BrowserTab.Create("page-second", second), BrowserTab.Create("home-tab")]);
var serialized = BrowserSessionSnapshotSerializer.Serialize(snapshot);
var restored = BrowserSessionSnapshotSerializer.Deserialize(serialized);

if (restored.Version != BrowserSessionSnapshot.CurrentVersion ||
    restored.SelectedTabId != "page-second" ||
    !restored.Tabs.Select(tab => tab.Id).SequenceEqual(["page-first", "page-second", "home-tab"]) ||
    restored.Tabs[2].Page is not null)
{
    throw new InvalidOperationException("The normal-mode Browser session snapshot must round-trip its version, selected tab, and public pages.");
}

using var document = JsonDocument.Parse(serialized);
var root = document.RootElement;
var rootProperties = root.EnumerateObject().Select(property => property.Name).Order().ToArray();
if (!rootProperties.SequenceEqual(["selectedTabId", "tabs", "version"]) ||
    root.GetProperty("tabs")[0].EnumerateObject().Select(property => property.Name).Order().ToArray()
        .SequenceEqual(["id", "page"]) is false ||
    root.GetProperty("tabs")[0].GetProperty("page").EnumerateObject().Select(property => property.Name).Order().ToArray()
        .SequenceEqual(["details", "title", "url"]) is false)
{
    throw new InvalidOperationException("The persisted Browser session contract must contain only versioned normal-mode public page metadata.");
}

var migrated = BrowserSessionSnapshotSerializer.Deserialize(
    "{\"version\":1,\"selectedTabId\":\"legacy-tab\",\"pages\":[{\"id\":\"legacy-tab\",\"url\":\"https://example.com/legacy?private=1#fragment\",\"title\":\"Legacy\",\"details\":\"Public\"}]}");
if (migrated.Version != BrowserSessionSnapshot.CurrentVersion ||
    migrated.Tabs.Count != 1 ||
    migrated.Tabs[0].Page?.Url != "https://example.com/legacy")
{
    throw new InvalidOperationException("Version 1 public pages must migrate to version 2 tabs with sanitized public URLs.");
}

var rejectedUnknownVersion = false;
try
{
    _ = BrowserSessionSnapshotSerializer.Deserialize("{\"version\":99,\"selectedTabId\":\"page-first\",\"pages\":[]}");
}
catch (InvalidDataException)
{
    rejectedUnknownVersion = true;
}

if (!rejectedUnknownVersion)
{
    throw new InvalidOperationException("The Browser session snapshot must reject unsupported schema versions.");
}

var temporaryRoot = Path.Combine(Path.GetTempPath(), $"browser-session-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    var store = new BrowserFileSessionStore(Path.Combine(temporaryRoot, "browser-session.json"));
    await store.SaveAsync(serialized, CancellationToken.None);
    if (await store.LoadAsync(CancellationToken.None) != serialized ||
        Directory.EnumerateFiles(temporaryRoot).Any(path => path.EndsWith(".tmp", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("The Browser session store must atomically publish one complete document without stale temporary files.");
    }

    var rejectedOversized = false;
    try
    {
        await store.SaveAsync(new string('x', BrowserFileSessionStore.MaximumSerializedBytes + 1), CancellationToken.None);
    }
    catch (InvalidDataException)
    {
        rejectedOversized = true;
    }

    if (!rejectedOversized)
    {
        throw new InvalidOperationException("The Browser session store must reject documents larger than 256KiB.");
    }
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}

Console.WriteLine("PASS: Browser session v2 tabs, v1 migration, public metadata, 256KiB bound, and atomic file replacement.");
