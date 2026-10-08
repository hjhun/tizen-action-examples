using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;
using RPCPort.DisplayActions;
using RPCPort.DisplayActions.Stub;

namespace DisplayPresentation.ActionProvider;

/// <summary>
/// The canonical Nudge action is unavailable; rendering stays in the app.
/// </summary>
public sealed class DisplayPresentationService : TizenActionPresentation.ServiceBase
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

    public override TizenEntityStatus ShowNudge(TizenEntityNudge nudge) =>
        Failure("unavailable: ShowNudge is not implemented");

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason ?? string.Empty };
}
