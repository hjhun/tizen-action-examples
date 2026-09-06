#nullable enable
using Reminder.UseCases;
using RPCPort.ReminderScheduleActionProvider.Stub;

namespace Reminder.ScheduleActionProvider;

public static class ReminderScheduleActionProviderHost
{
    private static TizenActionReminder? _stub;
    private static RPCPort.ReminderCustomActionProvider.Stub.TizenActionReminderCustom? _custom;

    public static void Start(ScheduleService service)
    {
        ProviderState.Service = service ?? throw new ArgumentNullException(nameof(service));
        _stub ??= new TizenActionReminder();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(ReminderScheduleService));
        _custom ??= new RPCPort.ReminderCustomActionProvider.Stub.TizenActionReminderCustom();
        if (!_custom.GetListenStatus()) _custom.Listen(typeof(ReminderCustomService));
    }
}

internal static class ProviderState
{
    internal static ScheduleService? Service { get; set; }
}
