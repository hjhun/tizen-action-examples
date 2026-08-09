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

var snapshot = BrowserSessionSnapshot.Create("page-second", [first, second]);
var serialized = BrowserSessionSnapshotSerializer.Serialize(snapshot);
var restored = BrowserSessionSnapshotSerializer.Deserialize(serialized);

if (restored.Version != BrowserSessionSnapshot.CurrentVersion ||
    restored.SelectedTabId != "page-second" ||
    !restored.Pages.Select(page => page.Id).SequenceEqual(["page-first", "page-second"]))
{
    throw new InvalidOperationException("The normal-mode Browser session snapshot must round-trip its version, selected tab, and public pages.");
}

using var document = JsonDocument.Parse(serialized);
var root = document.RootElement;
var rootProperties = root.EnumerateObject().Select(property => property.Name).Order().ToArray();
if (!rootProperties.SequenceEqual(["pages", "selectedTabId", "version"]) ||
    root.GetProperty("pages")[0].EnumerateObject().Select(property => property.Name).Order().ToArray()
        .SequenceEqual(["details", "id", "title", "url"]) is false)
{
    throw new InvalidOperationException("The persisted Browser session contract must contain only versioned normal-mode public page metadata.");
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

Console.WriteLine("PASS: Browser normal-mode session snapshot is versioned, bounded to public metadata, and round-trips selected tab state.");
