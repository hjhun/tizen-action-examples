using Browser.Domain;
using Browser.Persistence;
using Browser.UseCases;

var page = BrowserPage.Create(
    id: "page-first",
    url: "https://www.example.com/first",
    title: "First page",
    details: "Public page summary");
var snapshot = BrowserSessionSnapshot.Create("page-first", [page]);

var restoreStore = new InMemorySessionStore(BrowserSessionSnapshotSerializer.Serialize(snapshot));
await using (var restoreCoordinator = new BrowserSessionCoordinator(restoreStore))
{
    var restored = await restoreCoordinator.RestoreAsync(CancellationToken.None);
    if (restored.Status != BrowserSessionRestoreStatus.Restored ||
        restored.Snapshot?.SelectedTabId != "page-first")
    {
        throw new InvalidOperationException("A stored normal-mode Browser session must restore through the portable use-case seam.");
    }
}

var saveStore = new DelayedFirstSaveStore();
await using (var saveCoordinator = new BrowserSessionCoordinator(saveStore))
{
    var firstSave = saveCoordinator.SaveAsync(snapshot, CancellationToken.None);
    await saveStore.FirstSaveStarted.Task;

    var secondSave = saveCoordinator.SaveAsync(snapshot, CancellationToken.None);
    saveStore.ReleaseFirstSave();

    var firstResult = await firstSave;
    var secondResult = await secondSave;
    if (firstResult.Status != BrowserSessionSaveStatus.Superseded ||
        secondResult.Status != BrowserSessionSaveStatus.Saved ||
        saveStore.SaveCount != 2)
    {
        throw new InvalidOperationException("A newer Browser session save must suppress the stale completion while allowing only one store write at a time.");
    }
}

Console.WriteLine("PASS: Browser session use cases restore public state and suppress stale serialized save completions.");

var navigationRuntime = new DelayedFirstNavigationRuntime();
await using (var navigationCoordinator = new BrowserNavigationCoordinator(navigationRuntime))
{
    var firstNavigation = navigationCoordinator.NavigateAsync(
        "page-first", "https://www.example.com/first", CancellationToken.None);
    await navigationRuntime.FirstNavigationStarted.Task;

    var secondNavigation = navigationCoordinator.NavigateAsync(
        "page-second", "https://www.example.com/second", CancellationToken.None);
    navigationRuntime.ReleaseFirstNavigation();

    var firstNavigationResult = await firstNavigation;
    var secondNavigationResult = await secondNavigation;
    if (firstNavigationResult.Status != BrowserNavigationStatus.Superseded ||
        secondNavigationResult.Status != BrowserNavigationStatus.Loaded ||
        navigationCoordinator.CurrentPage?.Id != "page-second" ||
        navigationRuntime.NavigationCount != 2)
    {
        throw new InvalidOperationException("A newer Browser navigation must suppress stale metadata and retain the latest loaded page.");
    }
}

var failedNavigationRuntime = new FailingNavigationRuntime();
await using (var failedNavigationCoordinator = new BrowserNavigationCoordinator(failedNavigationRuntime))
{
    var failedNavigation = await failedNavigationCoordinator.NavigateAsync(
        "page-failed", "https://www.example.com/failure", CancellationToken.None);
    if (failedNavigation.Status != BrowserNavigationStatus.Failed ||
        failedNavigation.Page is not null ||
        failedNavigation.Error != "offline")
    {
        throw new InvalidOperationException("A web runtime failure must be returned without publishing a current Browser page.");
    }
}

Console.WriteLine("PASS: Browser navigation use case serializes runtime work, suppresses stale completion, and exposes bounded failures.");

sealed class InMemorySessionStore(string? serialized) : IBrowserSessionStore
{
    public Task<string?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(serialized);

    public Task SaveAsync(string serializedSnapshot, CancellationToken cancellationToken) => Task.CompletedTask;
}

sealed class DelayedFirstSaveStore : IBrowserSessionStore
{
    private readonly TaskCompletionSource _firstSaveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstSave = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource FirstSaveStarted => _firstSaveStarted;

    public int SaveCount { get; private set; }

    public Task<string?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public async Task SaveAsync(string serializedSnapshot, CancellationToken cancellationToken)
    {
        SaveCount++;
        if (SaveCount == 1)
        {
            _firstSaveStarted.SetResult();
            await _releaseFirstSave.Task;
        }
    }

    public void ReleaseFirstSave() => _releaseFirstSave.SetResult();
}

sealed class DelayedFirstNavigationRuntime : IWebRuntime
{
    private readonly TaskCompletionSource _firstNavigationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstNavigation = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource FirstNavigationStarted => _firstNavigationStarted;

    public int NavigationCount { get; private set; }

    public async Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        NavigationCount++;
        if (NavigationCount == 1)
        {
            _firstNavigationStarted.SetResult();
            await _releaseFirstNavigation.Task.WaitAsync(cancellationToken);
        }

        return WebNavigationOutcome.Loaded($"Title for {uri.AbsolutePath}", "Public page summary");
    }

    public void ReleaseFirstNavigation() => _releaseFirstNavigation.SetResult();
}

sealed class FailingNavigationRuntime : IWebRuntime
{
    public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(WebNavigationOutcome.Failed("offline"));
}
