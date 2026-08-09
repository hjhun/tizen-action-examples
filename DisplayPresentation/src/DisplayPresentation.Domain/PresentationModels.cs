namespace DisplayPresentation.Domain;

public sealed record PresentationInput(string Template, string Document);

/// <summary>
/// A profile-validated, renderer-agnostic A2UI tree. It contains resolved bounded values only;
/// untrusted JSON never reaches NUI composition.
/// </summary>
public sealed record SemanticSurface(string SurfaceId, SemanticNode Root);

public abstract record SemanticNode(string Id);

public sealed record VerticalGroup(string Id, IReadOnlyList<SemanticNode> Children) : SemanticNode(Id);

public sealed record TextValue(string Id, string Role, string Value) : SemanticNode(Id);

public sealed record RenderPlan(SemanticSurface Surface);

public enum RenderFailureKind
{
    InvalidInput,
    Unsupported,
    Cancelled,
}

public sealed record RenderFailure(RenderFailureKind Kind, string Message);

public sealed record RenderOutcome(RenderPlan? Plan, RenderFailure? Failure)
{
    public bool IsSuccess => Plan is not null;

    public static RenderOutcome Success(RenderPlan plan) => new(plan, null);

    public static RenderOutcome Fail(RenderFailureKind kind, string message) => new(null, new(kind, message));
}

public readonly record struct Insets(float Start, float End, float Top, float Bottom);

public readonly record struct Viewport(float Scale, float OffsetX, float OffsetY, float Width, float Height)
{
    public static bool TryCreate(float windowWidth, float windowHeight, Insets insets, out Viewport viewport)
    {
        viewport = default;
        var availableWidth = windowWidth - insets.Start - insets.End;
        var availableHeight = windowHeight - insets.Top - insets.Bottom;
        if (!float.IsFinite(windowWidth) || !float.IsFinite(windowHeight) ||
            !float.IsFinite(availableWidth) || !float.IsFinite(availableHeight) ||
            windowWidth <= 0 || windowHeight <= 0 || availableWidth <= 0 || availableHeight <= 0)
        {
            return false;
        }

        var scale = MathF.Min(availableWidth / 1920f, availableHeight / 1080f);
        if (!float.IsFinite(scale) || scale <= 0)
        {
            return false;
        }

        var width = 1920f * scale;
        var height = 1080f * scale;
        viewport = new Viewport(scale, insets.Start + ((availableWidth - width) / 2f), insets.Top + ((availableHeight - height) / 2f), width, height);
        return true;
    }
}
