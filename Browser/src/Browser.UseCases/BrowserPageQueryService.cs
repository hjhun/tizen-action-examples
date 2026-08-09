using Browser.Domain;

namespace Browser.UseCases;

/// <summary>
/// Exposes the public Browser-page projection shared by the NUI application and Action adapters.
/// It intentionally observes only the current normal-mode page metadata owned by the navigation coordinator.
/// </summary>
public sealed class BrowserPageQueryService
{
    private readonly BrowserNavigationCoordinator _navigation;

    public BrowserPageQueryService(BrowserNavigationCoordinator navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public BrowserPage? GetCurrentPage() => _navigation.CurrentPage;

    public BrowserPageResolution ResolveByIds(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var currentPage = _navigation.CurrentPage;
        var pages = new List<BrowserPage>();
        var unresolvedIds = new List<string>();
        foreach (var id in ids)
        {
            if (currentPage is not null && string.Equals(currentPage.Id, id, StringComparison.Ordinal))
            {
                pages.Add(currentPage);
            }
            else
            {
                unresolvedIds.Add(id ?? string.Empty);
            }
        }

        return new BrowserPageResolution(pages, unresolvedIds);
    }
}
