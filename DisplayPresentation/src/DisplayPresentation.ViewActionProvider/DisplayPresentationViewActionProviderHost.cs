using RPCPort.DisplayActions;
using RPCPort.ViewActions.Stub;

namespace DisplayPresentation.ViewActionProvider;

/// <summary>Composition seam for the generated View category and current NUI surface snapshot.</summary>
public static class DisplayPresentationViewActionProviderHost
{
    private static TizenActionView _stub;

    public static void Start()
    {
        _stub ??= new TizenActionView();
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
        DisplayPresentationViewProviderState.Publish([new DisplayPresentationViewProviderState.PublishedPresentationView(
            $"display:{surfaceId}:surface", surfaceId, presentation.Template, presentation.Document,
            presentation.ToJson(), screenX, screenY, windowX, windowY, width, height, isFocused)]);
    }

    public static void PublishViews(IEnumerable<PresentationViewSnapshot> views) =>
        DisplayPresentationViewProviderState.Publish(views.Select(view =>
            new DisplayPresentationViewProviderState.PublishedPresentationView(view.Id, view.SurfaceId,
                view.Presentation.Template, view.Presentation.Document, view.Presentation.ToJson(),
                view.ScreenX, view.ScreenY, view.WindowX, view.WindowY, view.Width, view.Height,
                view.IsFocused, view.Description)));

    public static void ClearPublishedViews() => DisplayPresentationViewProviderState.Clear();
}

public sealed record PresentationViewSnapshot(string Id, string SurfaceId, string Description,
    TizenEntityPresentation Presentation, double ScreenX, double ScreenY, double? WindowX, double? WindowY,
    double Width, double Height, bool IsFocused);
