using DisplayPresentation.Domain;

Assert(Viewport.TryCreate(1920, 1080, new Insets(0, 0, 0, 0), out var reference) && reference.Scale == 1f && reference.OffsetX == 0f && reference.OffsetY == 0f, "Reference viewport must be unscaled.");
Assert(Viewport.TryCreate(1280, 720, new Insets(0, 0, 0, 0), out var hd) && Near(hd.Scale, 2f / 3f), "1280x720 must use uniform two-thirds scale.");
Assert(Viewport.TryCreate(1440, 1080, new Insets(0, 0, 0, 0), out var fourThree) && Near(fourThree.Scale, .75f) && Near(fourThree.OffsetY, 135f), "4:3 viewport must letterbox vertically.");
Assert(Viewport.TryCreate(2560, 1080, new Insets(0, 0, 0, 0), out var wide) && Near(wide.Scale, 1f) && Near(wide.OffsetX, 320f), "Ultrawide viewport must pillarbox horizontally.");
Assert(Viewport.TryCreate(1920, 1080, new Insets(10, 30, 20, 40), out var inset) && inset.OffsetX > 10f && inset.OffsetY == 20f, "Asymmetric insets must be included in canvas placement.");
Assert(!Viewport.TryCreate(0, 1080, new Insets(0, 0, 0, 0), out _), "Zero width must be rejected.");
Assert(!Viewport.TryCreate(100, 100, new Insets(50, 50, 0, 0), out _), "Insets that consume width must be rejected.");
Console.WriteLine("DisplayPresentation.Domain.Tests: PASS");

static bool Near(float value, float expected) => MathF.Abs(value - expected) < .001f;
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
