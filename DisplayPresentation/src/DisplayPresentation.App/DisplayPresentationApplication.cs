using DisplayPresentation.ActionProvider;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;
using DisplayPresentation.ViewActionProvider;
using DisplayEntity = RPCPort.DisplayActions.TizenEntityPresentation;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace DisplayPresentation.App;

/// <summary>
/// In-process composition root. The generated Display service and NUI surface share one
/// coordinator; the UI never calls its own Action RPC.
/// </summary>
internal sealed class DisplayPresentationApplication : NUIApplication
{
    private readonly PresentationRenderCoordinator _coordinator = new();
    private OneUiPresentationCanvas? _canvas;
    private SynchronizationContext? _uiContext;

    protected override void OnCreate()
    {
        base.OnCreate();
        _uiContext = SynchronizationContext.Current ?? throw new InvalidOperationException("NUI requires a UI synchronization context.");
        _coordinator.Rendered += OnRendered;
        DisplayPresentationActionProviderHost.Start(_coordinator);
        DisplayPresentationViewActionProviderHost.Start();
        Window.Default.Resized += OnWindowResized;
        Window.Default.InsetsChanged += OnWindowResized;
        Render(_coordinator.Current);
    }

    protected override void OnTerminate()
    {
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.Resized -= OnWindowResized;
        _coordinator.Rendered -= OnRendered;
        DisplayPresentationViewActionProviderHost.ClearPublishedViews();
        RemoveCanvas();
        base.OnTerminate();
    }

    private void OnRendered(object? sender, RenderOutcome outcome)
    {
        _uiContext?.Post(static state =>
        {
            var update = (RenderUpdate)state!;
            update.Application.Render(update.Outcome);
        }, new RenderUpdate(this, outcome));
    }

    private void OnWindowResized(object? sender, EventArgs eventArgs)
    {
        if (_canvas is not null)
        {
            UpdateReferenceCanvasLayout(_canvas.Canvas);
        }
    }

    private void Render(RenderOutcome outcome)
    {
        var replacement = new OneUiPresentationCanvas(outcome, DismissCurrentPresentation);
        if (!UpdateReferenceCanvasLayout(replacement.Canvas))
        {
            return;
        }

        RemoveCanvas();
        _canvas = replacement;
        Window.Default.GetDefaultLayer().Add(replacement.Canvas);
        PublishVisibleSurface(outcome, replacement.Canvas);
        if (replacement.RecoveryFocus is { } recovery)
        {
            FocusManager.Instance.SetCurrentFocusView(recovery);
        }
    }

    private bool UpdateReferenceCanvasLayout(View canvas)
    {
        var window = Window.Default.WindowSize;
        var insets = Window.Default.GetInsets();
        var availableWidth = window.Width - insets.Start - insets.End;
        var availableHeight = window.Height - insets.Top - insets.Bottom;
        if (!float.IsFinite(availableWidth) || !float.IsFinite(availableHeight) || availableWidth <= 0f || availableHeight <= 0f)
        {
            return false;
        }

        var scale = MathF.Min(availableWidth / OneUiPresentationCanvas.DesignWidth, availableHeight / OneUiPresentationCanvas.DesignHeight);
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return false;
        }

        canvas.Scale = new Vector3(scale, scale, 1f);
        canvas.Position = new Position(
            insets.Start + ((availableWidth - (OneUiPresentationCanvas.DesignWidth * scale)) / 2f),
            insets.Top + ((availableHeight - (OneUiPresentationCanvas.DesignHeight * scale)) / 2f));
        return true;
    }

    private void DismissCurrentPresentation()
    {
        _coordinator.Dismiss();
    }

    private void RemoveCanvas()
    {
        if (_canvas is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_canvas.Canvas);
            _canvas = null;
        }
    }

    private sealed record RenderUpdate(DisplayPresentationApplication Application, RenderOutcome Outcome);

    private static void PublishVisibleSurface(RenderOutcome outcome, View canvas)
    {
        if (outcome.Plan is not { } plan)
        {
            DisplayPresentationViewActionProviderHost.ClearPublishedViews();
            return;
        }

        try
        {
            var bounds = canvas.CalculateScreenPositionSize();
            var width = bounds.Z;
            var height = bounds.W;
            if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(width) || !float.IsFinite(height) ||
                width <= 0f || height <= 0f)
            {
                DisplayPresentationViewActionProviderHost.ClearPublishedViews();
                return;
            }

            double? windowX = null;
            double? windowY = null;
            try
            {
                using var windowPosition = Window.Default.WindowPosition;
                windowX = bounds.X - windowPosition.X;
                windowY = bounds.Y - windowPosition.Y;
            }
            catch
            {
                // Screen bounds remain valid when the platform has no window-origin seam.
            }

            var presentation = A2UiPresentationSerializer.Serialize(plan.Surface);
            DisplayPresentationViewActionProviderHost.PublishVisibleSurface(
                plan.Surface.SurfaceId,
                new DisplayEntity { Template = presentation.Template, Document = presentation.Document },
                bounds.X, bounds.Y, windowX, windowY, width, height, isFocused: false);
        }
        catch
        {
            // A just-replaced actor can lack stable geometry for this frame; never publish stale bounds.
            DisplayPresentationViewActionProviderHost.ClearPublishedViews();
        }
    }

    private static void Main(string[] args)
    {
        var app = new DisplayPresentationApplication();
        app.Run(args);
    }
}
