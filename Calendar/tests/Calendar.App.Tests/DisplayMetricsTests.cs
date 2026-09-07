using Calendar.App;

internal static class DisplayMetricsTests
{
    private static int _checks;

    internal static void Run()
    {
        _checks = 0;
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

        foreach (var (width, height) in new[] { (-1, 1080), (1920, -1), (0, 1080), (1920, 0) })
            Check(!CalendarDisplayMetrics.TryCreate(width, height, 7680, 4320, 0, 0, 0, 0, out _));

        foreach (var (width, height) in new[] { (0, 1080), (1920, 0), (-1, 1080), (1920, -1) })
        {
            Check(CalendarDisplayMetrics.TryCreate(1920, 1080, width, height, 0, 0, 0, 0, out var invalidScreen));
            Check(!invalidScreen.HasScreenSize && invalidScreen.ScreenWidth == 0 && invalidScreen.ScreenHeight == 0);
            Check(invalidScreen.Viewport.Scale == 1);
        }

        foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            for (var axis = 0; axis < 4; axis++)
            {
                var insets = new float[4];
                insets[axis] = invalid;
                Check(!CalendarDisplayMetrics.TryCreate(1920, 1080, 1920, 1080,
                    insets[0], insets[1], insets[2], insets[3], out _));
            }
            Check(!ProportionalViewport.TryCreate(invalid, 1080, out _));
            Check(!ProportionalViewport.TryCreate(1920, invalid, out _));
        }
        Check(!CalendarDisplayMetrics.TryCreate(1920, 1080, 1920, 1080, 0, 600, 0, 480, out _));
        Check(!CalendarDisplayMetrics.TryCreate(1920, 1080, 1920, 1080, 0, 600, 0, 481, out _));

        // 920 reference units remain vertically. DCI has 256 additional physical
        // horizontal pixels, hence an extra 128 offset; it must not stretch.
        foreach (var (width, height, factor, extraX) in new[]
        {
            (1920, 1080, 1f, 0f), (3840, 2160, 2f, 0f),
            (4096, 2160, 2f, 128f), (7680, 4320, 4f, 0f),
        })
        {
            Check(CalendarDisplayMetrics.TryCreate(width, height, 1280, 720,
                40 * factor, 60 * factor, 80 * factor, 100 * factor, out var insetMetrics));
            var actual = insetMetrics.Viewport;
            Console.WriteLine($"INSET_CASE {width}x{height} factor={factor} actual={actual}");
            Check(Math.Abs(actual.Scale - 23d / 27 * factor) < 0.00001);
            Check(Math.Abs(actual.OffsetX - (1100d / 9 * factor + extraX)) < 0.002);
            Check(Math.Abs(actual.OffsetY - 60 * factor) < 0.002);
            Check(Math.Abs(actual.ContentWidth - 14720d / 9 * factor) < 0.002);
            Check(Math.Abs(actual.ContentHeight - 920 * factor) < 0.002);
            // Float multiplication can overshoot a content edge by a fraction of a pixel.
            // Use the same 0.002 physical-pixel tolerance on all four containment edges.
            Check(actual.OffsetX >= 40 * factor - 0.002 && actual.OffsetY >= 60 * factor - 0.002);
            Check(actual.OffsetX + actual.ContentWidth <= width - 80 * factor + 0.002);
            Check(actual.OffsetY + actual.ContentHeight <= height - 100 * factor + 0.002);
            Check(insetMetrics.ScreenWidth == 1280 && insetMetrics.ScreenHeight == 720);
        }

        var reminder = Calendar.Domain.CalendarReminder.Create("r", "Call", DateTimeOffset.Now, "notes")
            .WithState("Blocked");
        var editor = CalendarReminderEditorState.CreateExisting(reminder).WithTitle("Updated title");
        Check(editor.ToDomain("unused").State == "Blocked");
        Console.WriteLine($"DISPLAY_METRICS_PASS checks={_checks}");
    }

    private static void Check(bool condition,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException($"Display metrics regression: {expression}");
    }
}
