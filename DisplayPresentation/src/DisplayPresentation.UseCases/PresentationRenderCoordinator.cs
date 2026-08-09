using DisplayPresentation.Domain;

namespace DisplayPresentation.UseCases;

/// <summary>
/// Owns the one active immutable render result shared by the typed Display provider and NUI app.
/// The provider supplies already-untrusted strings; only profile-validated semantic trees are
/// published to subscribers. A newer request always replaces the prior visible result.
/// </summary>
public sealed class PresentationRenderCoordinator
{
    private readonly object _gate = new();
    private readonly A2UiPresentationParser _parser;
    private long _requestId;
    private RenderOutcome _current = RenderOutcome.Fail(RenderFailureKind.InvalidInput, "No presentation is currently available.");

    public PresentationRenderCoordinator(A2UiPresentationParser? parser = null)
    {
        _parser = parser ?? new A2UiPresentationParser();
    }

    public event EventHandler<RenderOutcome>? Rendered;

    public RenderOutcome Current
    {
        get { lock (_gate) return _current; }
    }

    public RenderOutcome Submit(PresentationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var requestId = Interlocked.Increment(ref _requestId);
        var outcome = _parser.Parse(input, cancellationToken);

        lock (_gate)
        {
            if (requestId != _requestId)
            {
                return RenderOutcome.Fail(RenderFailureKind.Cancelled, "Rendering was superseded by a newer request.");
            }
            _current = outcome;
        }

        Rendered?.Invoke(this, outcome);
        return outcome;
    }

    /// <summary>
    /// Dismisses a profile-owned failure/recovery surface. No prior payload is resurrected:
    /// callers must submit a new Presentation to render content again.
    /// </summary>
    public RenderOutcome Dismiss()
    {
        var outcome = RenderOutcome.Fail(RenderFailureKind.InvalidInput, "No presentation is currently available.");
        Interlocked.Increment(ref _requestId);
        lock (_gate)
        {
            _current = outcome;
        }

        Rendered?.Invoke(this, outcome);
        return outcome;
    }
}
