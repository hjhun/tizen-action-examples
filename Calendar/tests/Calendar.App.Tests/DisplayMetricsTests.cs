using Calendar.App;

internal static class DisplayMetricsTests
{
    internal static void Run()
    {
        foreach (var (width, height, scale) in new[]
        {
            (1280, 720, 2f / 3f), (1920, 1080, 1f), (2560, 1440, 4f / 3f),
            (3840, 2160, 2f), (7680, 4320, 4f),
        })
        {
            Check(CalendarDisplayMetrics.TryCreate(width, height, width, height, 0, 0, 0, 0, out var metrics));
            Check(Math.Abs(metrics.Viewport.Scale - scale) < 0.0001f);
            Check(metrics.Viewport.OffsetX == 0 && metrics.Viewport.OffsetY == 0);
            Check(Math.Abs(24 * metrics.Viewport.Scale - 24 * scale) < 0.001f);
        }

        Check(CalendarDisplayMetrics.TryCreate(1920, 1080, 7680, 4320, 0, 0, 0, 0, out var windowed));
        Check(windowed.ScreenWidth == 7680 && windowed.WindowWidth == 1920);
        Check(windowed.Viewport.Scale == 1);
        Check(CalendarDisplayMetrics.TryCreate(3840, 2160, 0, 0, 40, 60, 80, 100, out var noScreen));
        Check(!noScreen.HasScreenSize);
        Check(noScreen.Viewport.OffsetX >= 40 && noScreen.Viewport.OffsetY >= 60);
        Check(noScreen.Viewport.OffsetX + noScreen.Viewport.ContentWidth <= 3840 - 80);
        Check(noScreen.Viewport.OffsetY + noScreen.Viewport.ContentHeight <= 2160 - 100);
        Check(!CalendarDisplayMetrics.TryCreate(0, 0, 7680, 4320, 0, 0, 0, 0, out _));
        Check(!CalendarDisplayMetrics.TryCreate(1920, 1080, 7680, 4320, 960, 0, 960, 0, out _));
        Check(!ProportionalViewport.TryCreate(float.NaN, 1080, out _));
        Check(!ProportionalViewport.TryCreate(1920, float.PositiveInfinity, out _));
        Check(!ProportionalViewport.TryCreate(float.Epsilon, float.Epsilon, out _));
        Check(CalendarDisplayMetrics.TryCreate(4096, 2160, 4096, 2160, 0, 0, 0, 0, out var cinema));
        Check(cinema.Viewport.Scale == 2 && cinema.Viewport.OffsetX == 128 && cinema.Viewport.OffsetY == 0);

        var reminder = Calendar.Domain.CalendarReminder.Create("r", "Call", DateTimeOffset.Now, "notes")
            .WithState("Blocked");
        var editor = CalendarReminderEditorState.CreateExisting(reminder).WithTitle("Updated title");
        Check(editor.ToDomain("unused").State == "Blocked");
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Display metrics regression.");
    }
}
