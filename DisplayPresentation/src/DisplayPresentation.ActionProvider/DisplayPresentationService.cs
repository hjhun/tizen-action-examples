using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;
using RPCPort.DisplayActions;
using RPCPort.DisplayActions.Stub;

namespace DisplayPresentation.ActionProvider;

/// <summary>
/// Typed boundary for <c>Tv_Tizen.Action.Display_Show</c>. Rendering is added by the
/// application composition root; this boundary validates the wire payload first.
/// </summary>
public sealed class DisplayPresentationService : TizenActionDisplay.ServiceBase
{
    private readonly A2UiPresentationParser _parser;

    public DisplayPresentationService()
        : this(new A2UiPresentationParser())
    {
    }

    internal DisplayPresentationService(A2UiPresentationParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus Show(TizenEntityPresentation presentation)
    {
        if (presentation is null)
        {
            return Failure("A Presentation payload is required.");
        }

        var outcome = _parser.Parse(new PresentationInput(presentation.Template, presentation.Document));
        return outcome.IsSuccess
            ? Success()
            : Failure(outcome.Failure!.Message);
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason ?? string.Empty };
}
