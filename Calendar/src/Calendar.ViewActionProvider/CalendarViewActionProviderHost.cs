#nullable enable

using Calendar.Domain;
using RPCPort.CalendarViewActionProvider.Stub;

namespace Calendar.ViewActionProvider;

public sealed record CalendarEventViewSnapshot(
    CalendarEvent Event,
    double ScreenX,
    double ScreenY,
    double? WindowX,
    double? WindowY,
    double Width,
    double Height);

public static class CalendarViewActionProviderHost
{
    private static TizenActionView? _stub;

    public static void Start()
    {
        _stub ??= new TizenActionView();
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
