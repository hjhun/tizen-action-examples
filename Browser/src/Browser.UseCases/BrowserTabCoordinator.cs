using Browser.Domain;
using Browser.Persistence;

namespace Browser.UseCases;

public sealed class BrowserTabCoordinator
{
    private readonly object _sync = new();
    private BrowserTabWorkspace _current;

    public BrowserTabCoordinator(BrowserTabWorkspace initialWorkspace)
    {
        _current = initialWorkspace ?? throw new ArgumentNullException(nameof(initialWorkspace));
    }

    public event EventHandler<BrowserTabWorkspace>? StateChanged;

    public BrowserTabWorkspace Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Restore(BrowserSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Publish(BrowserTabWorkspace.Restore(snapshot.Tabs, snapshot.SelectedTabId));
    }

    public void OpenTabs() => Mutate(workspace => workspace.OpenTabs());

    public bool TryCloseTabs()
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (_current.Surface != BrowserWorkspaceSurface.Tabs)
            {
                return false;
            }

            updated = _current.CloseTabs();
            _current = updated;
        }

        Notify(updated);
        return true;
    }

    public bool TryCreateTab(out string createdTabId)
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (!_current.TryCreateTab(out updated, out createdTabId))
            {
                return false;
            }

            _current = updated;
        }

        Notify(updated);
        return true;
    }

    public bool TrySelectTab(string tabId, out BrowserTab selectedTab)
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (_current.Tabs.All(tab => tab.Id != tabId))
            {
                selectedTab = null!;
                return false;
            }

            updated = _current.SelectTab(tabId);
            _current = updated;
            selectedTab = updated.SelectedTab;
        }

        Notify(updated);
        return true;
    }

    public bool TryRequestClose(string tabId)
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (!_current.TryRequestClose(tabId, out updated))
            {
                return false;
            }

            _current = updated;
        }

        Notify(updated);
        return true;
    }

    public bool TryCancelClose()
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (_current.Surface != BrowserWorkspaceSurface.CloseConfirmation)
            {
                return false;
            }

            updated = _current.CancelClose();
            _current = updated;
        }

        Notify(updated);
        return true;
    }

    public bool TryConfirmClose()
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            if (_current.Surface != BrowserWorkspaceSurface.CloseConfirmation)
            {
                return false;
            }

            updated = _current.ConfirmClose();
            _current = updated;
        }

        Notify(updated);
        return true;
    }

    public void UpdateSelectedPage(BrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Mutate(workspace => workspace.UpdatePage(page));
    }

    public BrowserSessionSnapshot CreateSnapshot()
    {
        var workspace = Current;
        return BrowserSessionSnapshot.CreateTabs(workspace.SelectedTabId, workspace.Tabs);
    }

    internal void Replace(BrowserTabWorkspace workspace) => Publish(workspace);

    private void Mutate(Func<BrowserTabWorkspace, BrowserTabWorkspace> mutation)
    {
        BrowserTabWorkspace updated;
        lock (_sync)
        {
            updated = mutation(_current);
            _current = updated;
        }

        Notify(updated);
    }

    private void Publish(BrowserTabWorkspace workspace)
    {
        lock (_sync)
        {
            _current = workspace;
        }

        Notify(workspace);
    }

    private void Notify(BrowserTabWorkspace workspace)
    {
        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<BrowserTabWorkspace> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, workspace);
            }
            catch
            {
                // One presentation/persistence observer cannot break tab state or another observer.
            }
        }
    }
}

public sealed record BrowserCreateTabResult(bool Succeeded, string? CreatedTabId);

public sealed record BrowserSelectTabResult(bool Succeeded, BrowserTab? SelectedTab);

public sealed class BrowserTabPersistenceCoordinator
{
    private readonly BrowserTabCoordinator _tabs;
    private readonly BrowserSessionCoordinator _sessions;
    private readonly SemaphoreSlim _commitGate = new(1, 1);

    public BrowserTabPersistenceCoordinator(BrowserTabCoordinator tabs, BrowserSessionCoordinator sessions)
    {
        _tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public Task<BrowserCreateTabResult> CreateTabAsync(CancellationToken cancellationToken) =>
        CreateTabAsync(fromHome: false, cancellationToken);

    public Task<BrowserCreateTabResult> CreateHomeTabAsync(CancellationToken cancellationToken) =>
        CreateTabAsync(fromHome: true, cancellationToken);

    private async Task<BrowserCreateTabResult> CreateTabAsync(bool fromHome, CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _tabs.Current;
            BrowserTabWorkspace desired;
            string createdTabId;
            var created = fromHome
                ? current.TryCreateHomeTab(out desired, out createdTabId)
                : current.TryCreateTab(out desired, out createdTabId);
            if (!created)
            {
                return new BrowserCreateTabResult(false, null);
            }

            await PersistThenPublishAsync(desired, cancellationToken).ConfigureAwait(false);
            return new BrowserCreateTabResult(true, createdTabId);
        }
        finally
        {
            _commitGate.Release();
        }
    }

    public async Task<BrowserSelectTabResult> SelectTabAsync(string tabId, CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _tabs.Current;
            if (current.Tabs.All(tab => tab.Id != tabId))
            {
                return new BrowserSelectTabResult(false, null);
            }

            var desired = current.SelectTab(tabId);
            await PersistThenPublishAsync(desired, cancellationToken).ConfigureAwait(false);
            return new BrowserSelectTabResult(true, desired.SelectedTab);
        }
        finally
        {
            _commitGate.Release();
        }
    }

    public async Task<bool> ConfirmCloseAsync(CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _tabs.Current;
            if (current.Surface != BrowserWorkspaceSurface.CloseConfirmation)
            {
                return false;
            }

            await PersistThenPublishAsync(current.ConfirmClose(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _commitGate.Release();
        }
    }

    public async Task<bool> UpdateSelectedPageAsync(BrowserPage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        await _commitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _tabs.Current;
            if (current.SelectedTabId != page.Id)
            {
                return false;
            }

            await PersistThenPublishAsync(current.UpdatePage(page), cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _commitGate.Release();
        }
    }

    public Task<BrowserSessionSaveResult> SaveCurrentAsync(CancellationToken cancellationToken) =>
        _sessions.SaveAsync(_tabs.CreateSnapshot(), cancellationToken);

    private async Task PersistThenPublishAsync(BrowserTabWorkspace desired, CancellationToken cancellationToken)
    {
        var snapshot = BrowserSessionSnapshot.CreateTabs(desired.SelectedTabId, desired.Tabs);
        var result = await _sessions.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (result.Status != BrowserSessionSaveStatus.Saved)
        {
            throw new InvalidOperationException("A tab mutation was superseded before its desired state was durable.");
        }

        _tabs.Replace(desired);
    }
}
