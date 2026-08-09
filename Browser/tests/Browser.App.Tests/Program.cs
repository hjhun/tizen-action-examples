using Browser.App;

var matrix = new[]
{
    new ViewportCase(1920, 1080, 0, 0, 0, 0, 1.0f, 0, 0),
    new ViewportCase(1280, 720, 0, 0, 0, 0, 2.0f / 3.0f, 0, 0),
    new ViewportCase(1440, 1080, 0, 0, 0, 0, 0.75f, 0, 135),
    new ViewportCase(2560, 1080, 0, 0, 0, 0, 1.0f, 320, 0),
    new ViewportCase(1920, 1080, 30, 20, 10, 40, 0.9444444f, 63.3333f, 20),
};

foreach (var item in matrix)
{
    if (!ReferenceCanvasViewport.TryCreate(
            item.WindowWidth,
            item.WindowHeight,
            item.InsetStart,
            item.InsetTop,
            item.InsetEnd,
            item.InsetBottom,
            out var viewport) ||
        !Near(viewport.Scale, item.Scale) ||
        !Near(viewport.OffsetX, item.OffsetX) ||
        !Near(viewport.OffsetY, item.OffsetY))
    {
        throw new InvalidOperationException($"Unexpected viewport for {item}: {viewport}");
    }
}

foreach (var invalid in new[]
         {
             new[] { 0f, 1080f, 0f, 0f, 0f, 0f },
             new[] { 1920f, -1f, 0f, 0f, 0f, 0f },
             new[] { 100f, 100f, 50f, 0f, 50f, 0f },
             new[] { float.NaN, 1080f, 0f, 0f, 0f, 0f },
         })
{
    if (ReferenceCanvasViewport.TryCreate(invalid[0], invalid[1], invalid[2], invalid[3], invalid[4], invalid[5], out _))
    {
        throw new InvalidOperationException("Invalid drawable geometry must retain the prior native frame.");
    }
}

if (BrowserShellMetrics.HeaderHeight != 132 ||
    BrowserShellMetrics.ContextHeight != 92 ||
    BrowserShellMetrics.ProgressHeight != 6 ||
    BrowserShellMetrics.ContentLeft != 52 ||
    BrowserShellMetrics.ContentTop != 230 ||
    BrowserShellMetrics.ContentWidth != 1816 ||
    BrowserShellMetrics.ContentHeight != 806 ||
    BrowserShellMetrics.ContentTop + BrowserShellMetrics.ContentHeight != 1036)
{
    throw new InvalidOperationException("NUI shell geometry must match the executable 1920x1080 Browser contract.");
}

var unavailableHistory = BrowserShellFocusGraph.Create(backEnabled: false, forwardEnabled: false);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, -1), BrowserShellFocusTarget.Reload);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Address, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Reload);
AssertFocus(unavailableHistory.MoveHorizontal(BrowserShellFocusTarget.Tabs, 1), BrowserShellFocusTarget.Tabs);
AssertFocus(unavailableHistory.MoveDown(BrowserShellFocusTarget.Address), BrowserShellFocusTarget.WebContent);
AssertFocus(unavailableHistory.MoveUp(BrowserShellFocusTarget.WebContent), BrowserShellFocusTarget.Address);

var backOnly = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: false);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Back);
AssertFocus(backOnly.MoveHorizontal(BrowserShellFocusTarget.Back, 1), BrowserShellFocusTarget.Reload);

var fullHistory = BrowserShellFocusGraph.Create(backEnabled: true, forwardEnabled: true);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Reload, -1), BrowserShellFocusTarget.Forward);
AssertFocus(fullHistory.MoveHorizontal(BrowserShellFocusTarget.Forward, -1), BrowserShellFocusTarget.Back);

Console.WriteLine("PASS: Browser NUI shell geometry, safe viewport, disabled-skip focus graph, and WebView vertical focus contract.");

static bool Near(float actual, float expected) => MathF.Abs(actual - expected) < 0.002f;

static void AssertFocus(BrowserShellFocusTarget actual, BrowserShellFocusTarget expected)
{
    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected focus {expected}, got {actual}.");
    }
}

internal sealed record ViewportCase(
    float WindowWidth,
    float WindowHeight,
    float InsetStart,
    float InsetTop,
    float InsetEnd,
    float InsetBottom,
    float Scale,
    float OffsetX,
    float OffsetY);
