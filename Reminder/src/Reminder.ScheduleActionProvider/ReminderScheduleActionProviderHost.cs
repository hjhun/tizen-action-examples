#nullable enable
using Reminder.UseCases;
using RPCPort.ReminderScheduleActionProvider.Stub;

namespace Reminder.ScheduleActionProvider;

public static class ReminderScheduleActionProviderHost
{
    private static TizenActionSchedule? _stub;

    public static void Start(ScheduleService service)
    {
        ProviderState.Service = service ?? throw new ArgumentNullException(nameof(service));
        _stub ??= new TizenActionSchedule();
        if (!_stub.GetListenStatus()) _stub.Listen(typeof(ReminderScheduleService));
    }
}

internal static class ProviderState
{
    internal static ScheduleService? Service { get; set; }
}
