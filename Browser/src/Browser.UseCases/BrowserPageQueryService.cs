using Browser.Domain;

namespace Browser.UseCases;

/// <summary>
/// Exposes the public Browser-page projection shared by the NUI application and Action adapters.
/// It intentionally observes only the current normal-mode page metadata owned by the navigation coordinator.
/// </summary>
public sealed class BrowserPageQueryService
{
    private readonly BrowserAgentStateRegistry _agentState;

    public BrowserPageQueryService(BrowserAgentStateRegistry agentState)
    {
        _agentState = agentState ?? throw new ArgumentNullException(nameof(agentState));
    }

    public BrowserAgentSurface CurrentSurface => _agentState.Current.Surface;

    public BrowserAgentSnapshot GetCurrentSnapshot() => _agentState.Current;

    public BrowserPage? GetCurrentPage() => _agentState.Current.Page;

    public BrowserPageResolution ResolveByIds(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var currentPage = GetCurrentSnapshot().Page;
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
