#nullable enable

using Calendar.Domain;
using RPCPort.ViewActionProvider.Stub;

namespace Calendar.ViewActionProvider;

public sealed record CalendarEventViewSnapshot(
    CalendarEvent Event,
    double X,
    double Y,
    double Width,
    double Height);

public static class CalendarViewActionProviderHost
{
    private static TizenInternalActionView? _stub;

    public static void Start()
    {
        _stub ??= new TizenInternalActionView();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(CalendarViewService));
        }
    }

    public static void PublishVisibleEventViews(IEnumerable<CalendarEventViewSnapshot> visibleViews, string? focusedEventId)
    {
        CalendarViewProviderState.PublishVisibleEventViews(visibleViews, focusedEventId);
    }

    public static void ClearPublishedViews() =>
        CalendarViewProviderState.PublishVisibleEventViews([], focusedEventId: null);
}
