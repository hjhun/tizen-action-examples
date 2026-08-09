using System.Text.Json;
using Browser.Domain;

namespace Browser.Persistence;

public sealed record BrowserSessionSnapshot
{
    public const int CurrentVersion = 1;
    private const int MaximumPageCount = 20;

    private BrowserSessionSnapshot(int version, string selectedTabId, IReadOnlyList<BrowserPage> pages)
    {
        Version = version;
        SelectedTabId = selectedTabId;
        Pages = pages;
    }

    public int Version { get; }

    public string SelectedTabId { get; }

    public IReadOnlyList<BrowserPage> Pages { get; }

    public static BrowserSessionSnapshot Create(string selectedTabId, IEnumerable<BrowserPage> pages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedTabId);
        ArgumentNullException.ThrowIfNull(pages);

        var pageList = pages.ToList();
        if (pageList.Count is 0 or > MaximumPageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pages), $"A normal-mode session must contain between 1 and {MaximumPageCount} public pages.");
        }

        if (pageList.Any(page => page is null))
        {
            throw new ArgumentException("A normal-mode session cannot contain a null page.", nameof(pages));
        }

        if (!pageList.Any(page => page.Id == selectedTabId))
        {
            throw new ArgumentException("The selected tab must be included in the normal-mode session pages.", nameof(selectedTabId));
        }

        return new BrowserSessionSnapshot(CurrentVersion, selectedTabId, pageList);
    }
}

public static class BrowserSessionSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(BrowserSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var persisted = new PersistedSession(
            snapshot.Version,
            snapshot.SelectedTabId,
            snapshot.Pages.Select(page => new PersistedPage(page.Id, page.Url, page.Title, page.Details)).ToArray());
        return JsonSerializer.Serialize(persisted, SerializerOptions);
    }

    public static BrowserSessionSnapshot Deserialize(string serialized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialized);

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedSession>(serialized, SerializerOptions)
                ?? throw new InvalidDataException("Browser session snapshot is empty.");
            if (persisted.Version != BrowserSessionSnapshot.CurrentVersion)
            {
                throw new InvalidDataException($"Unsupported Browser session snapshot version '{persisted.Version}'.");
            }

            if (persisted.Pages is null)
            {
                throw new InvalidDataException("Browser session snapshot pages are required.");
            }

            return BrowserSessionSnapshot.Create(
                persisted.SelectedTabId,
                persisted.Pages.Select(page => BrowserPage.Create(page.Id, page.Url, page.Title, page.Details)));
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

    private sealed record PersistedSession(int Version, string SelectedTabId, PersistedPage[]? Pages);

    private sealed record PersistedPage(string Id, string Url, string Title, string Details);
}
