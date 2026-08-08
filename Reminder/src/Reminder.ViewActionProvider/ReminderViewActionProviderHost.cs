#nullable enable
using Reminder.Domain;
using RPCPort.ReminderViewActionProvider.Stub;

namespace Reminder.ViewActionProvider;

public sealed record ReminderViewSnapshot(
    ReminderItem? Reminder,
    ReservationItem? Reservation,
    double ScreenX,
    double ScreenY,
    double? WindowX,
    double? WindowY,
    double Width,
    double Height,
    string ViewId,
    bool IsFocused,
    bool IncludeNote = false);

public static class ReminderViewActionProviderHost
{
    private static TizenInternalActionView? _stub;
    public static void Start()
    {
        _stub ??= new TizenInternalActionView();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(ReminderViewService));
    }
    public static void Publish(IEnumerable<ReminderViewSnapshot> snapshots) => ReminderViewState.Publish(snapshots);
    public static void Clear() => ReminderViewState.Publish([]);
}
