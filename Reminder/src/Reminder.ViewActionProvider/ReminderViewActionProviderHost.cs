#nullable enable
using ActionExamples.ViewAnnotations;
using RPCPort.ReminderViewActionProvider.Stub;

namespace Reminder.ViewActionProvider;

public static class ReminderViewActionProviderHost
{
    private static TizenActionView? _stub;
    public static void Start()
    {
        _stub ??= new TizenActionView();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(ReminderViewService));
    }
    public static void Publish(IEnumerable<CurrentViewSnapshot> views) => ReminderViewProviderState.Store.Publish(views);
    public static void ClearPublishedViews() => ReminderViewProviderState.Store.Clear();
}
