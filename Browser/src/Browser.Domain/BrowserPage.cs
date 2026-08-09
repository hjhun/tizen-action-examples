namespace Browser.Domain;

public sealed record BrowserPage
{
    private const int MaximumIdLength = 256;
    private const int MaximumUrlLength = 4_096;
    private const int MaximumTitleLength = 512;
    private const int MaximumDetailsLength = 2_048;

    private BrowserPage(string id, string url, string title, string details)
    {
        Id = id;
        Url = url;
        Title = title;
        Details = details;
    }

    public string Id { get; }

    public string Url { get; }

    public string Title { get; }

    public string Details { get; }

    public static BrowserPage Create(string id, string url, string title, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(details);

        if (id.Length > MaximumIdLength || url.Length > MaximumUrlLength ||
            title.Length > MaximumTitleLength || details.Length > MaximumDetailsLength)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Browser page fields exceed their bounded lengths.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Browser page URL must be an absolute HTTP or HTTPS URL.", nameof(url));
        }

        return new BrowserPage(id, uri.AbsoluteUri, title, details);
    }
}

public sealed record BrowserPageResolution(IReadOnlyList<BrowserPage> Pages, IReadOnlyList<string> UnresolvedIds);

public sealed class BrowserPageCatalog
{
    private readonly IReadOnlyDictionary<string, BrowserPage> _pagesById;

    public BrowserPageCatalog(IEnumerable<BrowserPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var pageMap = new Dictionary<string, BrowserPage>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);
            if (!pageMap.TryAdd(page.Id, page))
            {
                throw new ArgumentException($"Duplicate browser page ID '{page.Id}' is not permitted.", nameof(pages));
            }
        }

        _pagesById = pageMap;
    }

    public BrowserPageResolution ResolveByIds(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var pages = new List<BrowserPage>();
        var unresolvedIds = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !_pagesById.TryGetValue(id, out var page))
            {
                unresolvedIds.Add(id ?? string.Empty);
                continue;
            }

            pages.Add(page);
        }

        return new BrowserPageResolution(pages, unresolvedIds);
    }
}
