namespace ActionExamples.Ui;

// A committed service change is delivered asynchronously to the owning UI loop.
// Bursts share a pending callback; callbacks queued before termination are inert.
internal sealed class UiChangeDispatcher : IDisposable
{
    private readonly SynchronizationContext _context;
    private readonly Action _refresh;
    private readonly object _gate = new();
    private bool _queued, _disposed;

    internal UiChangeDispatcher(SynchronizationContext context, Action refresh)
    { _context = context; _refresh = refresh; }

    internal void Request()
    {
        lock (_gate)
        {
            if (_disposed || _queued) return;
            _queued = true;
        }
        _context.Post(_ =>
        {
            lock (_gate)
            {
                _queued = false;
                if (_disposed) return;
            }
            _refresh();
        }, null);
    }

    public void Dispose() { lock (_gate) _disposed = true; }
}
