#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using RPCPort.ScheduleReminderActionProvider.Stub;

namespace Calendar.ScheduleActionProvider;

public static class ScheduleReminderActionProviderHost
{
    private static TizenActionSchedule? _stub;

    public static void Start(CalendarReminderRepository reminders, CalendarCommandService commands)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(commands);
        ScheduleProviderState.Configure(reminders, commands);

        _stub ??= new TizenActionSchedule();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(ScheduleReminderService));
        }
    }
}

internal static class ScheduleProviderState
{
    internal static CalendarReminderRepository Reminders { get; private set; } = new([]);
    internal static CalendarCommandService? Commands { get; private set; }

    internal static void Configure(CalendarReminderRepository reminders, CalendarCommandService commands)
    {
        Reminders = reminders;
        Commands = commands;
    }
}
