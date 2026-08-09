using RPCPort.DisplayActions;
using RPCPort.ViewActions.Stub;

namespace DisplayPresentation.ViewActionProvider;

/// <summary>Composition seam for the generated View category and current NUI surface snapshot.</summary>
public static class DisplayPresentationViewActionProviderHost
{
    private static TizenInternalActionView _stub;

    public static void Start()
    {
        _stub ??= new TizenInternalActionView();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(DisplayPresentationViewService));
        }
    }

    public static void PublishVisibleSurface(
        string surfaceId,
        TizenEntityPresentation presentation,
        double screenX,
        double screenY,
        double? windowX,
        double? windowY,
        double width,
        double height,
        bool isFocused)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentNullException.ThrowIfNull(presentation);
        DisplayPresentationViewProviderState.Publish(new DisplayPresentationViewProviderState.PublishedPresentationView(
            $"display:{surfaceId}:surface", surfaceId, presentation.Template, presentation.Document,
            presentation.ToJson(), screenX, screenY, windowX, windowY, width, height, isFocused));
    }

    public static void ClearPublishedViews() => DisplayPresentationViewProviderState.Clear();
}