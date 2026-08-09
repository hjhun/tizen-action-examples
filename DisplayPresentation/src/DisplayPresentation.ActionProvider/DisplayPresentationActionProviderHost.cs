using DisplayPresentation.UseCases;
using RPCPort.DisplayActions.Stub;

namespace DisplayPresentation.ActionProvider;

/// <summary>
/// Starts the complete generated Display category using the same render coordinator as NUI.
/// </summary>
public static class DisplayPresentationActionProviderHost
{
    private static TizenActionDisplay _stub;

    public static void Start(PresentationRenderCoordinator renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        DisplayPresentationActionProviderState.Configure(renderer);
        _stub ??= new TizenActionDisplay();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(DisplayPresentationService));
        }
    }
}

internal static class DisplayPresentationActionProviderState
{
    private static PresentationRenderCoordinator _renderer;

    internal static PresentationRenderCoordinator Renderer =>
        _renderer ?? throw new InvalidOperationException("DisplayPresentation Action provider has not been configured.");

    internal static void Configure(PresentationRenderCoordinator renderer) => _renderer = renderer;
}