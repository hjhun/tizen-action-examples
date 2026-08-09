using System.Text;
using System.Text.Json;
using Browser.Domain;

namespace Browser.Persistence;

public sealed record BrowserSessionSnapshot
{
    public const int CurrentVersion = 2;

    private BrowserSessionSnapshot(string selectedTabId, IReadOnlyList<BrowserTab> tabs)
    {
        Version = CurrentVersion;
        SelectedTabId = selectedTabId;
        Tabs = tabs;
    }

    public int Version { get; }

    public string SelectedTabId { get; }

    public IReadOnlyList<BrowserTab> Tabs { get; }

    public IReadOnlyList<BrowserPage> Pages => Tabs.Where(tab => tab.Page is not null).Select(tab => tab.Page!).ToArray();

    public static BrowserSessionSnapshot Create(string selectedTabId, IEnumerable<BrowserPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return CreateTabs(selectedTabId, pages.Select(page => BrowserTab.Create(page.Id, page)));
    }

    public static BrowserSessionSnapshot CreateTabs(string selectedTabId, IEnumerable<BrowserTab> tabs)
    {
        var workspace = BrowserTabWorkspace.Restore(tabs, selectedTabId);
        return new BrowserSessionSnapshot(workspace.SelectedTabId, workspace.Tabs);
    }
}

public static class BrowserSessionSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(BrowserSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var persisted = new PersistedSessionV2(
            snapshot.Version,
            snapshot.SelectedTabId,
            snapshot.Tabs.Select(tab => new PersistedTab(
                tab.Id,
                tab.Page is null
                    ? null
                    : new PersistedPage(tab.Page.Url, tab.Page.Title, tab.Page.Details))).ToArray());
        var serialized = JsonSerializer.Serialize(persisted, SerializerOptions);
        EnsureBounded(serialized);
        return serialized;
    }

    public static BrowserSessionSnapshot Deserialize(string serialized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialized);
        EnsureBounded(serialized);

        try
        {
            using var document = JsonDocument.Parse(serialized);
            if (!document.RootElement.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
            {
                throw new InvalidDataException("Browser session snapshot version is required.");
            }

            return version switch
            {
                BrowserSessionSnapshot.CurrentVersion => DeserializeCurrent(serialized),
                1 => MigrateVersionOne(serialized),
                _ => throw new InvalidDataException($"Unsupported Browser session snapshot version '{version}'."),
            };
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException("Browser session snapshot is invalid.", exception);
        }
    }

    private static BrowserSessionSnapshot DeserializeCurrent(string serialized)
    {
        var persisted = JsonSerializer.Deserialize<PersistedSessionV2>(serialized, SerializerOptions)
            ?? throw new InvalidDataException("Browser session snapshot is empty.");
        if (persisted.Tabs is null)
        {
            throw new InvalidDataException("Browser session snapshot tabs are required.");
        }

        return BrowserSessionSnapshot.CreateTabs(
            persisted.SelectedTabId,
            persisted.Tabs.Select(tab => BrowserTab.Create(
                tab.Id,
                tab.Page is null
                    ? null
                    : BrowserPage.Create(tab.Id, tab.Page.Url, tab.Page.Title, tab.Page.Details))));
    }

    private static BrowserSessionSnapshot MigrateVersionOne(string serialized)
    {
        var persisted = JsonSerializer.Deserialize<PersistedSessionV1>(serialized, SerializerOptions)
            ?? throw new InvalidDataException("Browser session snapshot is empty.");
        if (persisted.Pages is null)
        {
            throw new InvalidDataException("Version 1 Browser session pages are required.");
        }

        return BrowserSessionSnapshot.Create(
            persisted.SelectedTabId,
            persisted.Pages.Select(page => BrowserPage.Create(page.Id, page.Url, page.Title, page.Details)));
    }

    private static void EnsureBounded(string serialized)
    {
        if (Encoding.UTF8.GetByteCount(serialized) > BrowserFileSessionStore.MaximumSerializedBytes)
        {
            throw new InvalidDataException("Browser session snapshot exceeds 256KiB.");
        }
    }

    private sealed record PersistedSessionV2(int Version, string SelectedTabId, PersistedTab[]? Tabs);

    private sealed record PersistedTab(string Id, PersistedPage? Page);

    private sealed record PersistedPage(string Url, string Title, string Details);

    private sealed record PersistedSessionV1(int Version, string SelectedTabId, PersistedPageV1[]? Pages);

    private sealed record PersistedPageV1(string Id, string Url, string Title, string Details);
}
