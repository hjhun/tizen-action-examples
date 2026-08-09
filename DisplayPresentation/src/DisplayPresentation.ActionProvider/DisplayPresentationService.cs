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
    private readonly PresentationRenderCoordinator _renderer;

    public DisplayPresentationService()
        : this(DisplayPresentationActionProviderState.Renderer)
    {
    }

    internal DisplayPresentationService(PresentationRenderCoordinator renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
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

        var outcome = _renderer.Submit(new PresentationInput(presentation.Template, presentation.Document));
        return outcome.IsSuccess
            ? Success()
            : Failure(outcome.Failure!.Message);
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason ?? string.Empty };
}
