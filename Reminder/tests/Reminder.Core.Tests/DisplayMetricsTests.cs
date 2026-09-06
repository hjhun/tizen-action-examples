using Reminder.App;

internal static class DisplayMetricsTests
{
    internal static void Run()
    {
        foreach (var (width, height, scale, offsetX) in new[]
        {
            (1920, 1080, 1f, 0f), (3840, 2160, 2f, 0f),
            (4096, 2160, 2f, 128f), (7680, 4320, 4f, 0f),
        })
        {
            Check(ReminderDisplayMetrics.TryCreate(width, height, width, height, 0, 0, 0, 0, out var metrics), "valid display");
            Check(metrics.Viewport.Scale == scale && metrics.Viewport.OffsetX == offsetX && metrics.Viewport.OffsetY == 0, "uniform centered canvas");
            Check(metrics.Viewport.ContentWidth == 1920 * scale && metrics.Viewport.ContentHeight == 1080 * scale, "scaled content bounds");
        }
        Check(ReminderDisplayMetrics.TryCreate(1920, 1080, 7680, 4320, 0, 0, 0, 0, out var windowed), "window on 8K display");
        Check(windowed.HasScreenSize && windowed.ScreenWidth == 7680 && windowed.Viewport.Scale == 1, "screen capability must not enlarge a window");
        Check(ReminderDisplayMetrics.TryCreate(3840, 2160, 0, 0, 40, 60, 80, 100, out var inset), "SystemInfo unavailable");
        Check(!inset.HasScreenSize && inset.Viewport.OffsetX >= 40 && inset.Viewport.OffsetY >= 60, "safe inset start");
        Check(inset.Viewport.OffsetX + inset.Viewport.ContentWidth <= 3760 && inset.Viewport.OffsetY + inset.Viewport.ContentHeight <= 2060, "safe inset end");
        Check(!ReminderDisplayMetrics.TryCreate(0, 0, 7680, 4320, 0, 0, 0, 0, out _), "invalid window retains previous frame");
        Check(!ReminderDisplayMetrics.TryCreate(1920, 1080, 7680, 4320, 960, 0, 960, 0, out _), "exhausted insets");
        Check(!ProportionalViewport.TryCreate(float.NaN, 1080, out _), "NaN rejection");
        Check(!ProportionalViewport.TryCreate(1920, float.PositiveInfinity, out _), "infinity rejection");
        Check(!ProportionalViewport.TryCreate(float.Epsilon, float.Epsilon, out _), "zero scale after underflow rejection");
        Console.WriteLine("DisplayMetricsTests: PASS (FHD/UHD/DCI 4K/8K, window vs screen, insets, invalid geometry)");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
