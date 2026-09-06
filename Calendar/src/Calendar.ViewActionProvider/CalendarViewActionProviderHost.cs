#nullable enable
using ActionExamples.ViewAnnotations;
using RPCPort.CalendarViewActionProvider.Stub;

namespace Calendar.ViewActionProvider;

public static class CalendarViewActionProviderHost
{
    private static TizenActionView? _stub;
    public static void Start()
    {
        _stub ??= new TizenActionView();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(CalendarViewService));
    }
    public static void Publish(IEnumerable<CurrentViewSnapshot> views) => CalendarViewProviderState.Store.Publish(views);
    public static void ClearPublishedViews() => CalendarViewProviderState.Store.Clear();
}
