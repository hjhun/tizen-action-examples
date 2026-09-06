namespace Calendar.App;

// Physical display capability and the app's drawable window are different.
internal readonly record struct CalendarDisplayMetrics(
    int WindowWidth,
    int WindowHeight,
    int ScreenWidth,
    int ScreenHeight,
    ProportionalViewport Viewport)
{
    internal bool HasScreenSize => ScreenWidth > 0 && ScreenHeight > 0;

    internal static bool TryCreate(
        int windowWidth, int windowHeight, int screenWidth, int screenHeight,
        float insetStart, float insetTop, float insetEnd, float insetBottom,
        out CalendarDisplayMetrics metrics)
    {
        metrics = default;
        // Never substitute the screen for a minimized/transiently invalid window.
        if (!ProportionalViewport.TryCreate(windowWidth, windowHeight,
                insetStart, insetTop, insetEnd, insetBottom, out var viewport))
        {
            return false;
        }

        var hasScreen = screenWidth > 0 && screenHeight > 0;
        metrics = new(windowWidth, windowHeight,
            hasScreen ? screenWidth : 0, hasScreen ? screenHeight : 0, viewport);
        return true;
    }
}
