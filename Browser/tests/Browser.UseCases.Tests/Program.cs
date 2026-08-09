using Browser.Domain;
using Browser.Persistence;
using Browser.UseCases;
using System.Diagnostics;

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

await using (var invalidRestoreCoordinator = new BrowserSessionCoordinator(new InMemorySessionStore("{not-json")))
{
    var invalidRestore = await invalidRestoreCoordinator.RestoreAsync(CancellationToken.None);
    if (invalidRestore.Status != BrowserSessionRestoreStatus.InvalidSession || invalidRestore.Snapshot is not null)
    {
        throw new InvalidOperationException("Malformed session persistence must fail closed to Home without crashing.");
    }
}

var tabCoordinator = new BrowserTabCoordinator(BrowserTabWorkspace.Create("tab-1"));
var tabStateChanges = 0;
tabCoordinator.StateChanged += (_, _) => tabStateChanges++;
tabCoordinator.OpenTabs();
if (!tabCoordinator.TryCreateTab(out var newTabId) ||
    !tabCoordinator.TrySelectTab(newTabId, out var selectedHomeTab) ||
    selectedHomeTab.Page is not null ||
    tabCoordinator.Current.Surface != BrowserWorkspaceSurface.Page)
{
    throw new InvalidOperationException("Tab commands must use one shared state coordinator and select a privacy-safe Home tab.");
}

var selectedPage = BrowserPage.Create(newTabId, "https://example.com/current", "Current", "Public summary");
tabCoordinator.UpdateSelectedPage(selectedPage);
var tabSnapshot = tabCoordinator.CreateSnapshot();
if (tabSnapshot.SelectedTabId != newTabId || tabSnapshot.Tabs.Single(tab => tab.Id == newTabId).Page != selectedPage ||
    tabStateChanges < 3)
{
    throw new InvalidOperationException("Navigation and persistence must observe the same selected tab state.");
}

await using (var homeNavigation = new BrowserNavigationCoordinator(new HistoryNavigationRuntime()))
{
    await homeNavigation.NavigateInputAsync(newTabId, "https://example.com/current", CancellationToken.None);
    homeNavigation.ResetToHome();
    if (homeNavigation.CurrentState.Phase != BrowserNavigationPhase.Home || homeNavigation.CurrentPage is not null)
    {
        throw new InvalidOperationException("Selecting a Home tab must clear the previously selected public page projection.");
    }
}

Console.WriteLine("PASS: Browser tab coordinator shares selection/page state and malformed persistence fails closed.");

var persistFirstStore = new DelayedFirstSaveStore();
await using (var sessionCoordinator = new BrowserSessionCoordinator(persistFirstStore))
{
    var tabs = new BrowserTabCoordinator(BrowserTabWorkspace.Create("tab-1").OpenTabs());
    var persistedTabs = new BrowserTabPersistenceCoordinator(tabs, sessionCoordinator);
    var create = persistedTabs.CreateTabAsync(CancellationToken.None);
    await persistFirstStore.FirstSaveStarted.Task;
    if (tabs.Current.Tabs.Count != 1)
    {
        throw new InvalidOperationException("A tab mutation must not publish before its desired session snapshot is durable.");
    }

    persistFirstStore.ReleaseFirstSave();
    var result = await create;
    if (!result.Succeeded || tabs.Current.Tabs.Count != 2 || tabs.Current.SelectedTabId != result.CreatedTabId)
    {
        throw new InvalidOperationException("A durable tab mutation must publish exactly once after persistence succeeds.");
    }
}

Console.WriteLine("PASS: Browser tab mutations persist desired state before in-memory publication.");

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

if (!BrowserNavigationInput.TryNormalize("  tizen action  ", out var searchRequest, out _) ||
    !searchRequest.IsSearch ||
    searchRequest.NavigationUri.AbsoluteUri != "https://duckduckgo.com/?q=tizen%20action" ||
    searchRequest.PublicDisplayUri != "https://duckduckgo.com/")
{
    throw new InvalidOperationException("Search input must use one bounded HTTPS provider URL and a query-free public projection.");
}

if (BrowserNavigationPolicy.NavigationTimeout != TimeSpan.FromSeconds(15))
{
    throw new InvalidOperationException("The product navigation timeout contract must remain exactly 15 seconds.");
}

if (!BrowserNavigationInput.TryNormalize("https://example.com/path?secret=1#private", out var urlRequest, out _) ||
    urlRequest.IsSearch ||
    urlRequest.PublicDisplayUri != "https://example.com/path" ||
    BrowserNavigationInput.TryNormalize("https://user:password@example.com/", out _, out _) ||
    BrowserNavigationInput.TryNormalize("https://", out _, out _) ||
    BrowserNavigationInput.TryNormalize("ftp://example.com/", out _, out _) ||
    BrowserNavigationInput.TryNormalize(new string('x', 513), out _, out _))
{
    throw new InvalidOperationException("Navigation normalization must bound input, reject credentials, and redact query/fragment metadata.");
}

var cancellableRuntime = new CancellableFirstNavigationRuntime();
await using (var coordinator = new BrowserNavigationCoordinator(cancellableRuntime))
{
    var states = new List<BrowserNavigationPhase>();
    coordinator.StateChanged += (_, state) => states.Add(state.Phase);
    var first = coordinator.NavigateInputAsync("tab-1", "https://example.com/first", CancellationToken.None);
    await cancellableRuntime.FirstNavigationStarted.Task;
    var cancellationWatch = Stopwatch.StartNew();
    var second = coordinator.NavigateInputAsync("tab-1", "https://example.com/second?private=1", CancellationToken.None);
    if (!cancellableRuntime.FirstCancellationObserved.Task.IsCompleted || cancellationWatch.Elapsed > TimeSpan.FromMilliseconds(100))
    {
        throw new InvalidOperationException("A superseded request must receive cancellation within 100ms.");
    }

    var firstResult = await first;
    var secondResult = await second;
    if (firstResult.Status != BrowserNavigationStatus.Superseded ||
        secondResult.Status != BrowserNavigationStatus.Loaded ||
        coordinator.CurrentState.Phase != BrowserNavigationPhase.Page ||
        coordinator.CurrentState.PublicUrl != "https://example.com/second" ||
        coordinator.CurrentState.History != new WebHistoryAvailability(true, false) ||
        states.Count(phase => phase == BrowserNavigationPhase.Loading) != 2)
    {
        throw new InvalidOperationException("A newer intent must promptly cancel its predecessor and publish only query-free latest state.");
    }
}

foreach (var failure in new[]
         {
             (WebNavigationFailure.Network, BrowserNavigationPhase.Offline),
             (WebNavigationFailure.EngineUnavailable, BrowserNavigationPhase.EngineError),
             (WebNavigationFailure.Timeout, BrowserNavigationPhase.Timeout),
         })
{
    await using var coordinator = new BrowserNavigationCoordinator(new TypedFailureRuntime(failure.Item1));
    var result = await coordinator.NavigateInputAsync("tab-1", "https://example.com/failure", CancellationToken.None);
    if (result.Status != BrowserNavigationStatus.Failed || coordinator.CurrentState.Phase != failure.Item2)
    {
        throw new InvalidOperationException($"Failure {failure.Item1} must map to state {failure.Item2}.");
    }
}

await using (var coordinator = new BrowserNavigationCoordinator(
                 new TypedFailureRuntime(WebNavigationFailure.Network, new string('e', 400))))
{
    await coordinator.NavigateInputAsync("tab-1", "https://example.com/failure", CancellationToken.None);
    if (coordinator.CurrentState.Error?.Length != 256)
    {
        throw new InvalidOperationException("Runtime error presentation must be bounded to 256 characters.");
    }
}

var recoveryRuntime = new RecoverableNavigationRuntime();
await using (var coordinator = new BrowserNavigationCoordinator(recoveryRuntime))
{
    await coordinator.NavigateInputAsync("tab-1", "https://example.com/stable", CancellationToken.None);
    recoveryRuntime.Fail = true;
    await coordinator.NavigateInputAsync("tab-1", "https://example.com/offline", CancellationToken.None);
    if (!coordinator.DismissTransientState() ||
        coordinator.CurrentState.Phase != BrowserNavigationPhase.Page ||
        coordinator.CurrentPage?.Url != "https://example.com/stable")
    {
        throw new InvalidOperationException("Back from a recovery state must restore the prior stable public page.");
    }
}

await using (var coordinator = new BrowserNavigationCoordinator(new HistoryNavigationRuntime()))
{
    await coordinator.NavigateInputAsync("tab-1", " ", CancellationToken.None);
    if (coordinator.CurrentState.Phase != BrowserNavigationPhase.InvalidInput ||
        !coordinator.DismissTransientState() ||
        coordinator.CurrentState.Phase != BrowserNavigationPhase.Home)
    {
        throw new InvalidOperationException("Invalid input without a prior page must recover to Home without engine work.");
    }
}

var historyRuntime = new HistoryNavigationRuntime();
await using (var coordinator = new BrowserNavigationCoordinator(historyRuntime))
{
    await coordinator.NavigateInputAsync("tab-1", "https://example.com/current", CancellationToken.None);
    await coordinator.ReloadAsync(CancellationToken.None);
    await coordinator.GoBackAsync(CancellationToken.None);
    await coordinator.GoForwardAsync(CancellationToken.None);
    await coordinator.RetryAsync(CancellationToken.None);
    if (historyRuntime.Commands != "navigate,reload,back,forward,navigate" ||
        coordinator.CurrentState.Phase != BrowserNavigationPhase.Page)
    {
        throw new InvalidOperationException("Reload and one-step history must share the navigation state pipeline.");
    }
}

Console.WriteLine("PASS: Browser input, typed states/recovery, prompt cancellation, redaction, retry/history command pipeline.");

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

sealed class CancellableFirstNavigationRuntime : IWebRuntime
{
    private int _count;
    public TaskCompletionSource FirstNavigationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FirstCancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _count) == 1)
        {
            FirstNavigationStarted.TrySetResult();
            using var cancellationRegistration = cancellationToken.Register(() => FirstCancellationObserved.TrySetResult());
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        return WebNavigationOutcome.Loaded("Latest", "Public summary", uri, new WebHistoryAvailability(true, false));
    }

    public Task<WebNavigationOutcome> ReloadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<WebNavigationOutcome> GoBackAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<WebNavigationOutcome> GoForwardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

sealed class TypedFailureRuntime(WebNavigationFailure failure, string error = "bounded failure") : IWebRuntime
{
    public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(WebNavigationOutcome.Failed(failure, error));

    public Task<WebNavigationOutcome> ReloadAsync(CancellationToken cancellationToken) => NavigateAsync(new Uri("https://example.com/"), cancellationToken);
    public Task<WebNavigationOutcome> GoBackAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);
    public Task<WebNavigationOutcome> GoForwardAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);
}

sealed class HistoryNavigationRuntime : IWebRuntime
{
    private readonly List<string> _commands = [];
    public string Commands => string.Join(',', _commands);

    public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) => Complete("navigate", uri);
    public Task<WebNavigationOutcome> ReloadAsync(CancellationToken cancellationToken) => Complete("reload", new Uri("https://example.com/current"));
    public Task<WebNavigationOutcome> GoBackAsync(CancellationToken cancellationToken) => Complete("back", new Uri("https://example.com/previous"));
    public Task<WebNavigationOutcome> GoForwardAsync(CancellationToken cancellationToken) => Complete("forward", new Uri("https://example.com/current"));

    private Task<WebNavigationOutcome> Complete(string command, Uri uri)
    {
        _commands.Add(command);
        return Task.FromResult(WebNavigationOutcome.Loaded(command, "Public summary", uri, new WebHistoryAvailability(true, true)));
    }
}

sealed class RecoverableNavigationRuntime : IWebRuntime
{
    public bool Fail { get; set; }

    public Task<WebNavigationOutcome> NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(Fail
            ? WebNavigationOutcome.Failed(WebNavigationFailure.Network, "offline")
            : WebNavigationOutcome.Loaded("Stable", "Public summary", uri));
}
