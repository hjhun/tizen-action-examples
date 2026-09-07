using Calendar.Domain;
using DisplayPresentation.Domain;
using DisplayPresentation.UseCases;

internal static class PagingTests
{
    internal static void Run()
    {
        var parser = new A2UiPresentationParser();
        var start = DateTimeOffset.Parse("2026-09-06T10:00:00Z");
        foreach (var count in new[] { 0, 1, 2, 7, 100 })
        {
            var output = CalendarA2UiPresentations.Create(Enumerable.Range(0, count).Select(i =>
                CalendarEvent.Create($"event-{i}", $"Event {i}", start, start.AddHours(1), $"Note {i}", "Room")).ToArray());
            var surface = parser.Parse(new(output.Template, output.Document)).Plan!.Surface;
            var pages = PresentationPages.Create(surface);
            Check(pages.Count == Math.Max(1, count), "Four-field events must remain accessible as one event per page.");
            Check(pages.SelectMany(p => ProducerInteropTests.Texts(p.Root))
                .SequenceEqual(ProducerInteropTests.Texts(surface.Root)), "Paging must neither lose nor duplicate text.");
            foreach (var page in pages)
            {
                Check(ProducerInteropTests.Texts(page.Root).Count() <= 4 && page.Root is VerticalGroup,
                    "Every page must have a bounded, hierarchy-preserving tree.");
                var input = A2UiPresentationSerializer.Serialize(page);
                var restored = parser.Parse(input);
                Check(restored.IsSuccess && ProducerInteropTests.Texts(restored.Plan!.Surface.Root)
                    .SequenceEqual(ProducerInteropTests.Texts(page.Root)), "Visible-page snapshots must round trip.");
            }
            if (count > 1)
            {
                var first = A2UiPresentationSerializer.Serialize(pages[0]);
                Check(!first.Document.Contains("Note 1", StringComparison.Ordinal), "Off-page values must not leak into annotations.");
                Check(pages[0].Root is VerticalGroup { Children: [VerticalGroup { Id: "event-0" }] },
                    "The event grouping must survive a page slice.");
            }
        }
        Console.WriteLine("PagingTests: PASS (bounded pages, hierarchy, ordering, visible snapshots)");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
