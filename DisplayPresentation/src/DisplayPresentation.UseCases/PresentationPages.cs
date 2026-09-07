using DisplayPresentation.Domain;

namespace DisplayPresentation.UseCases;

/// <summary>Pages an already validated tree without exposing off-page values.</summary>
public static class PresentationPages
{
    public const int TextsPerPage = 4;

    public static IReadOnlyList<SemanticSurface> Create(SemanticSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var count = CountTexts(surface.Root);
        var pages = new List<SemanticSurface>();
        for (var first = 0; first < Math.Max(1, count); first += TextsPerPage)
        {
            var index = 0;
            var root = Slice(surface.Root, first, ref index) ?? new VerticalGroup(surface.Root.Id, []);
            pages.Add(new SemanticSurface(surface.SurfaceId, root));
        }
        return pages.AsReadOnly();
    }

    private static int CountTexts(SemanticNode node) => node switch
    {
        TextValue => 1,
        VerticalGroup group => group.Children.Sum(CountTexts),
        _ => throw new ArgumentException("Only validated semantic trees can be paged."),
    };

    private static SemanticNode? Slice(SemanticNode node, int first, ref int index)
    {
        if (node is TextValue)
        {
            var position = index++;
            return position >= first && position < first + TextsPerPage ? node : null;
        }
        var group = (VerticalGroup)node;
        var children = new List<SemanticNode>();
        foreach (var child in group.Children)
        {
            var visible = Slice(child, first, ref index);
            if (visible is not null) children.Add(visible);
        }
        return children.Count == 0 ? null : new VerticalGroup(group.Id, children.AsReadOnly());
    }
}
