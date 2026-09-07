using ActionExamples.Ui;

internal static class UiChangeDispatcherTests
{
    internal static void Run()
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var context = new QueuedContext();
        var rendered = 0;
        using var dispatcher = new UiChangeDispatcher(context, () =>
        {
            if (Environment.CurrentManagedThreadId != uiThread) throw new Exception("UI ran on provider thread");
            rendered++;
        });
        Task.Run(() => { for (var i = 0; i < 100; i++) dispatcher.Request(); }).GetAwaiter().GetResult();
        if (rendered != 0 || context.Pending.Count != 1) throw new Exception("Burst must post one UI refresh without inline rendering");
        context.Drain();
        if (rendered != 1) throw new Exception("Committed changes were dropped");
        dispatcher.Request(); context.Drain();
        if (rendered != 2) throw new Exception("Later changes must schedule a new frame");
        dispatcher.Request(); dispatcher.Dispose(); context.Drain(); dispatcher.Request();
        if (rendered != 2 || context.Pending.Count != 0) throw new Exception("Termination must cancel queued and future callbacks");
        Console.WriteLine("UiChangeDispatcherTests PASS: provider thread, coalescing, subsequent changes, termination");
    }
    private sealed class QueuedContext : SynchronizationContext
    {
        internal Queue<Action> Pending = new();
        public override void Post(SendOrPostCallback d, object? state) => Pending.Enqueue(() => d(state));
        internal void Drain() { while (Pending.TryDequeue(out var callback)) callback(); }
    }
}
