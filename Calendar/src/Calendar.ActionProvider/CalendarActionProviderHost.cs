#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using RPCPort.CalendarActionProvider.Stub;

namespace Calendar.ActionProvider;

public static class CalendarActionProviderHost
{
    private static TizenActionCalendar? _stub;

    public static void Start(CalendarEventRepository repository, CalendarCommandService commands)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(commands);
        CalendarProviderState.Configure(repository, commands);

        _stub ??= new TizenActionCalendar();
        if (!_stub.GetListenStatus())
        {
            _stub.Listen(typeof(CalendarService));
        }
    }
}

internal static class CalendarProviderState
{
    private static CalendarEventRepository _repository = new([]);
    private static CalendarCommandService? _commands;

    internal static CalendarEventRepository Repository => _repository;
    internal static CalendarCommandService? Commands => _commands;

    internal static void Configure(CalendarEventRepository repository, CalendarCommandService commands)
    {
        _repository = repository;
        _commands = commands;
    }
}
