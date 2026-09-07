using DisplayPresentation.ActionProvider;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;
using DisplayPresentation.ViewActionProvider;
using DisplayEntity = RPCPort.DisplayActions.TizenEntityPresentation;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace DisplayPresentation.App;

internal sealed class DisplayPresentationApplication : NUIApplication
{
    private readonly PresentationRenderCoordinator _coordinator = new();
    private OneUiPresentationCanvas? _canvas;
    private View? _root;
    private SynchronizationContext? _uiContext;
    private Tizen.NUI.Timer? _annotationTimer;
    private IReadOnlyList<SemanticSurface> _pages = [];
    private RenderOutcome? _shownOutcome;
    private int _page;
    private bool _paused;
    private bool _terminated;
    private bool _renderPending;
    private SemanticSurface? _visibleSurface;
    private int _visiblePage;
    private int _visiblePageCount;

    protected override void OnCreate()
    {
        base.OnCreate();
        _uiContext = SynchronizationContext.Current ?? throw new InvalidOperationException("NUI requires a UI synchronization context.");
        _annotationTimer = new Tizen.NUI.Timer(50);
        _annotationTimer.Tick += (_, _) => { PublishVisiblePage(); return false; };
        _coordinator.Rendered += OnRendered;
        DisplayPresentationActionProviderHost.Start(_coordinator);
        DisplayPresentationViewActionProviderHost.Start();
        Window.Default.Resized += OnWindowResized;
        Window.Default.InsetsChanged += OnWindowResized;
        Window.Default.Moved += OnWindowMoved;
        Window.Default.KeyEvent += OnKeyEvent;
        FocusManager.Instance.FocusChanged += OnFocusChanged;
        ShowCurrent();
    }

    protected override void OnPause()
    {
        _paused = true;
        _annotationTimer?.Stop();
        DisplayPresentationViewActionProviderHost.ClearPublishedViews();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _paused = false;
        ShowCurrent();
    }

    protected override void OnTerminate()
    {
        _terminated = true;
        Window.Default.Resized -= OnWindowResized;
        Window.Default.InsetsChanged -= OnWindowResized;
        Window.Default.Moved -= OnWindowMoved;
        Window.Default.KeyEvent -= OnKeyEvent;
        FocusManager.Instance.FocusChanged -= OnFocusChanged;
        _coordinator.Rendered -= OnRendered;
        _coordinator.Dismiss();
        _annotationTimer?.Stop();
        _annotationTimer?.Dispose();
        _annotationTimer = null;
        RemoveCanvas();
        base.OnTerminate();
    }

    private void OnRendered(object? sender, RenderOutcome outcome) => _uiContext?.Post(_ =>
    {
        // Delivery can be reordered by concurrent provider calls or lifecycle transitions.
        if (!_paused && !_terminated && ReferenceEquals(outcome, _coordinator.Current)) ShowCurrent();
    }, null);

    private void ShowCurrent()
    {
        if (_paused || _terminated) return;
        var outcome = _coordinator.Current;
        if (!ReferenceEquals(outcome, _shownOutcome))
        {
            _shownOutcome = outcome;
            _pages = outcome.Plan is { } plan ? PresentationPages.Create(plan.Surface) : [];
            _page = 0;
        }
        RenderPage();
    }

    private void ChangePage(int delta)
    {
        var next = Math.Clamp(_page + delta, 0, Math.Max(0, _pages.Count - 1));
        if (next == _page) return;
        _page = next;
        RenderPage(delta < 0 ? "previous" : "next");
    }

    private void RenderPage(string? preferredControl = null)
    {
        if (_paused || _terminated || _shownOutcome is null) return;
        if (!TryViewport(out var viewport)) { _renderPending = true; return; }
        _renderPending = false;
        var pageOutcome = _pages.Count == 0 ? _shownOutcome : RenderOutcome.Success(new RenderPlan(_pages[_page]));
        var replacement = new OneUiPresentationCanvas(pageOutcome, _page, _pages.Count, ChangePage,
            () => _coordinator.Dismiss(), preferredControl);
        ApplyViewport(replacement.Canvas, viewport);
        RemoveCanvas();
        _root = new View
        {
            WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent,
            BackgroundColor = new Color("#F7F7F8FF"), FocusableChildren = true,
        };
        _root.Add(replacement.Canvas);
        _canvas = replacement;
        _visibleSurface = pageOutcome.Plan?.Surface;
        _visiblePage = _page;
        _visiblePageCount = _pages.Count;
        _root.Relayout += (_, _) => QueueAnnotations();
        Window.Default.GetDefaultLayer().Add(_root);
        FocusManager.Instance.SetCurrentFocusView(replacement.InitialFocus);
        QueueAnnotations();
    }

    private void OnWindowResized(object? sender, EventArgs args)
    {
        if (_paused || _terminated || !TryViewport(out var viewport)) return;
        if (_canvas is null || _renderPending) { RenderPage(); return; }
        ApplyViewport(_canvas.Canvas, viewport);
        QueueAnnotations();
    }

    private void OnWindowMoved(object? sender, EventArgs args) => QueueAnnotations();
    private void OnFocusChanged(object? sender, FocusManager.FocusChangedEventArgs args) => QueueAnnotations();

    private void OnKeyEvent(object? sender, Window.KeyEventArgs args)
    {
        if (_paused || _terminated || args.Key.State != Key.StateType.Down) return;
        switch (args.Key.KeyPressedName)
        {
            case "XF86Back": case "Escape": _coordinator.Dismiss(); break;
            case "Down": case "Right": _canvas?.MoveFocus(1); break;
            case "Up": case "Left": _canvas?.MoveFocus(-1); break;
        }
    }

    private static bool TryViewport(out Viewport viewport)
    {
        var size = Window.Default.WindowSize;
        var insets = Window.Default.GetInsets();
        return Viewport.TryCreate(size.Width, size.Height,
            new Insets(insets.Start, insets.End, insets.Top, insets.Bottom), out viewport);
    }

    private static void ApplyViewport(View canvas, Viewport viewport)
    {
        canvas.Scale = new Vector3(viewport.Scale, viewport.Scale, 1f);
        canvas.Position = new Position(viewport.OffsetX, viewport.OffsetY);
    }

    private void RemoveCanvas()
    {
        DisplayPresentationViewActionProviderHost.ClearPublishedViews();
        _annotationTimer?.Stop();
        _canvas = null;
        _visibleSurface = null;
        if (_root is not null)
        {
            Window.Default.GetDefaultLayer().Remove(_root);
            _root.Dispose();
            _root = null;
        }
    }

    private void QueueAnnotations()
    {
        DisplayPresentationViewActionProviderHost.ClearPublishedViews();
        _annotationTimer?.Stop();
        if (!_paused && !_terminated && _canvas is not null) _annotationTimer?.Start();
    }

    private void PublishVisiblePage()
    {
        if (_paused || _terminated || _canvas is null || _visibleSurface is not { } surface) return;
        var wire = A2UiPresentationSerializer.Serialize(surface);
        var entity = new DisplayEntity { Template = wire.Template, Document = wire.Document };
        var views = new List<PresentationViewSnapshot>();
        var focused = FocusManager.Instance.GetCurrentFocusView();
        void Capture(View view, string id, string description)
        {
            try
            {
                if (!view.Visibility) return;
                var bounds = view.CalculateScreenPositionSize();
                if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) ||
                    !float.IsFinite(bounds.Z) || !float.IsFinite(bounds.W) || bounds.Z <= 0 || bounds.W <= 0) return;
                double? windowX = null, windowY = null;
                try { using var origin = Window.Default.WindowPosition; windowX = bounds.X - origin.X; windowY = bounds.Y - origin.Y; }
                catch { /* The screen measurement remains valid without an optional window origin. */ }
                views.Add(new(id, surface.SurfaceId, description, entity, bounds.X, bounds.Y, windowX, windowY,
                    bounds.Z, bounds.W, view == focused));
            }
            catch { /* Replaced actors do not describe the current frame. */ }
        }
        var prefix = $"display:{surface.SurfaceId}:page:{_visiblePage}";
        Capture(_canvas.Content, prefix, $"Presentation page {_visiblePage + 1} of {_visiblePageCount}");
        foreach (var control in _canvas.Controls.Where(x => x.IsEnabled))
            Capture(control, prefix + ":control:" + control.Name, control.Text);
        DisplayPresentationViewActionProviderHost.PublishViews(views);
    }

    private static void Main(string[] args)
    {
        var app = new DisplayPresentationApplication();
        app.Run(args);
    }
}
