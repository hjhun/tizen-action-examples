namespace Reminder.App;

internal readonly record struct ProportionalViewport(
    float Scale,
    float OffsetX,
    float OffsetY,
    float ContentWidth,
    float ContentHeight)
{
    public const float ReferenceWidth = 1920.0f;
    public const float ReferenceHeight = 1080.0f;

    public static ProportionalViewport Create(
        float windowWidth,
        float windowHeight,
        float insetStart = 0.0f,
        float insetTop = 0.0f,
        float insetEnd = 0.0f,
        float insetBottom = 0.0f)
    {
        if (!TryCreate(windowWidth, windowHeight, insetStart, insetTop, insetEnd, insetBottom, out var viewport))
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth), "Window dimensions and insets must leave a finite positive content area.");
        }

        return viewport;
    }

    public static bool TryCreate(float windowWidth, float windowHeight, out ProportionalViewport viewport) =>
        TryCreate(windowWidth, windowHeight, 0.0f, 0.0f, 0.0f, 0.0f, out viewport);

    public static bool TryCreate(
        float windowWidth,
        float windowHeight,
        float insetStart,
        float insetTop,
        float insetEnd,
        float insetBottom,
        out ProportionalViewport viewport)
    {
        if (!float.IsFinite(windowWidth) || !float.IsFinite(windowHeight) || windowWidth <= 0.0f || windowHeight <= 0.0f ||
            !float.IsFinite(insetStart) || !float.IsFinite(insetTop) || !float.IsFinite(insetEnd) || !float.IsFinite(insetBottom) ||
            insetStart < 0.0f || insetTop < 0.0f || insetEnd < 0.0f || insetBottom < 0.0f)
        {
            viewport = default;
            return false;
        }

        var availableWidth = windowWidth - insetStart - insetEnd;
        var availableHeight = windowHeight - insetTop - insetBottom;
        if (availableWidth <= 0.0f || availableHeight <= 0.0f)
        {
            viewport = default;
            return false;
        }

        var scale = Math.Min(availableWidth / ReferenceWidth, availableHeight / ReferenceHeight);
        var contentWidth = ReferenceWidth * scale;
        var contentHeight = ReferenceHeight * scale;
        viewport = new ProportionalViewport(
            scale,
            insetStart + ((availableWidth - contentWidth) / 2.0f),
            insetTop + ((availableHeight - contentHeight) / 2.0f),
            contentWidth,
            contentHeight);
        return true;
    }
}
