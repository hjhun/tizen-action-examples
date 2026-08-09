namespace Browser.Domain;

public sealed record BrowserTab
{
    private BrowserTab(string id, BrowserPage? page)
    {
        Id = id;
        Page = page;
    }

    public string Id { get; }

    public BrowserPage? Page { get; }

    public static BrowserTab Create(string id, BrowserPage? page = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (page is not null && !string.Equals(page.Id, id, StringComparison.Ordinal))
        {
            throw new ArgumentException("A tab and its public page must share one stable ID.", nameof(page));
        }

        return new BrowserTab(id, page);
    }

    public BrowserTab WithPage(BrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Create(Id, page);
    }
}

public enum BrowserWorkspaceSurface
{
    Page,
    Tabs,
    CloseConfirmation,
}

public enum BrowserWorkspaceFocus
{
    Address,
    HomeQuickAccess,
    SelectedTab,
    InvokingClose,
    CancelClose,
}

public sealed record BrowserTabWorkspace
{
    public const int MaximumTabs = 20;

    private BrowserTabWorkspace(
        IReadOnlyList<BrowserTab> tabs,
        string selectedTabId,
        BrowserWorkspaceSurface surface,
        string? pendingCloseTabId,
        BrowserWorkspaceFocus preferredFocus,
        string? preferredFocusTabId)
    {
        Tabs = Array.AsReadOnly(tabs.ToArray());
        SelectedTabId = selectedTabId;
        Surface = surface;
        PendingCloseTabId = pendingCloseTabId;
        PreferredFocus = preferredFocus;
        PreferredFocusTabId = preferredFocusTabId;
    }

    public IReadOnlyList<BrowserTab> Tabs { get; }

    public string SelectedTabId { get; }

    public BrowserWorkspaceSurface Surface { get; }

    public string? PendingCloseTabId { get; }

    public BrowserWorkspaceFocus PreferredFocus { get; }

    public string? PreferredFocusTabId { get; }

    public bool CanCreateTab => Tabs.Count < MaximumTabs;

    public BrowserTab SelectedTab => Tabs.First(tab => tab.Id == SelectedTabId);

    public string PendingCloseTitle
    {
        get
        {
            var title = Tabs.FirstOrDefault(tab => tab.Id == PendingCloseTabId)?.Page?.Title;
            title = string.IsNullOrWhiteSpace(title) ? "New tab" : title;
            return title.Length <= 80 ? title : title[..80];
        }
    }

    public static BrowserTabWorkspace Create(string initialTabId) =>
        Restore([BrowserTab.Create(initialTabId)], initialTabId);

    public static BrowserTabWorkspace Restore(IEnumerable<BrowserTab> tabs, string selectedTabId)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedTabId);
        var materialized = tabs.ToArray();
        if (materialized.Length is 0 or > MaximumTabs)
        {
            throw new ArgumentOutOfRangeException(nameof(tabs));
        }

        if (materialized.Any(tab => tab is null) ||
            materialized.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException("Normal tabs must be non-null and have unique stable IDs.", nameof(tabs));
        }

        if (!materialized.Any(tab => tab.Id == selectedTabId))
        {
            throw new ArgumentException("The selected tab must exist in the workspace.", nameof(selectedTabId));
        }

        return new BrowserTabWorkspace(
            materialized,
            selectedTabId,
            BrowserWorkspaceSurface.Page,
            null,
            BrowserWorkspaceFocus.Address,
            null);
    }

    public BrowserTabWorkspace OpenTabs() => new(
        Tabs,
        SelectedTabId,
        BrowserWorkspaceSurface.Tabs,
        null,
        BrowserWorkspaceFocus.SelectedTab,
        SelectedTabId);

    public BrowserTabWorkspace CloseTabs() => new(
        Tabs,
        SelectedTabId,
        BrowserWorkspaceSurface.Page,
        null,
        BrowserWorkspaceFocus.Address,
        null);

    public bool TryCreateTab(out BrowserTabWorkspace workspace, out string createdTabId) =>
        TryCreateTab(BrowserWorkspaceSurface.Tabs, BrowserWorkspaceSurface.Tabs, BrowserWorkspaceFocus.SelectedTab, out workspace, out createdTabId);

    public bool TryCreateHomeTab(out BrowserTabWorkspace workspace, out string createdTabId) =>
        TryCreateTab(BrowserWorkspaceSurface.Page, BrowserWorkspaceSurface.Page, BrowserWorkspaceFocus.HomeQuickAccess, out workspace, out createdTabId);

    private bool TryCreateTab(
        BrowserWorkspaceSurface requiredSurface,
        BrowserWorkspaceSurface resultSurface,
        BrowserWorkspaceFocus resultFocus,
        out BrowserTabWorkspace workspace,
        out string createdTabId)
    {
        if (!CanCreateTab || Surface != requiredSurface)
        {
            workspace = this;
            createdTabId = string.Empty;
            return false;
        }

        string candidate;
        do
        {
            candidate = $"tab-{Guid.NewGuid():N}";
        }
        while (Tabs.Any(tab => tab.Id == candidate));

        createdTabId = candidate;
        var updatedTabs = Tabs.Append(BrowserTab.Create(candidate)).ToArray();
        workspace = new BrowserTabWorkspace(
            updatedTabs,
            createdTabId,
            resultSurface,
            null,
            resultFocus,
            createdTabId);
        return true;
    }

    public BrowserTabWorkspace SelectTab(string tabId)
    {
        EnsureTab(tabId);
        return new BrowserTabWorkspace(
            Tabs,
            tabId,
            BrowserWorkspaceSurface.Page,
            null,
            BrowserWorkspaceFocus.Address,
            null);
    }

    public BrowserTabWorkspace UpdatePage(BrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!string.Equals(page.Id, SelectedTabId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only the selected tab may accept the current navigation page.", nameof(page));
        }

        var updated = Tabs.Select(tab => tab.Id == SelectedTabId ? tab.WithPage(page) : tab).ToArray();
        return new BrowserTabWorkspace(
            updated,
            SelectedTabId,
            Surface,
            PendingCloseTabId,
            PreferredFocus,
            PreferredFocusTabId);
    }

    public bool TryRequestClose(string tabId, out BrowserTabWorkspace workspace)
    {
        if (Tabs.Count == 1 || Surface != BrowserWorkspaceSurface.Tabs || Tabs.All(tab => tab.Id != tabId))
        {
            workspace = this;
            return false;
        }

        workspace = new BrowserTabWorkspace(
            Tabs,
            SelectedTabId,
            BrowserWorkspaceSurface.CloseConfirmation,
            tabId,
            BrowserWorkspaceFocus.CancelClose,
            tabId);
        return true;
    }

    public BrowserTabWorkspace CancelClose()
    {
        EnsurePendingClose();
        return new BrowserTabWorkspace(
            Tabs,
            SelectedTabId,
            BrowserWorkspaceSurface.Tabs,
            null,
            BrowserWorkspaceFocus.InvokingClose,
            PendingCloseTabId);
    }

    public BrowserTabWorkspace ConfirmClose()
    {
        EnsurePendingClose();
        var removedIndex = Array.FindIndex(Tabs.ToArray(), tab => tab.Id == PendingCloseTabId);
        var remaining = Tabs.Where(tab => tab.Id != PendingCloseTabId).ToArray();
        var nearestIndex = Math.Min(removedIndex, remaining.Length - 1);
        var nearestId = remaining[nearestIndex].Id;
        var selectedId = SelectedTabId == PendingCloseTabId ? nearestId : SelectedTabId;
        return new BrowserTabWorkspace(
            remaining,
            selectedId,
            BrowserWorkspaceSurface.Tabs,
            null,
            BrowserWorkspaceFocus.SelectedTab,
            nearestId);
    }

    private void EnsureTab(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        if (Tabs.All(tab => tab.Id != tabId))
        {
            throw new ArgumentException("The requested tab does not exist.", nameof(tabId));
        }
    }

    private void EnsurePendingClose()
    {
        if (Surface != BrowserWorkspaceSurface.CloseConfirmation ||
            string.IsNullOrEmpty(PendingCloseTabId) ||
            Tabs.All(tab => tab.Id != PendingCloseTabId))
        {
            throw new InvalidOperationException("There is no pending tab close confirmation.");
        }
    }
}
