#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using RPCPort.CalendarActionProvider;
using RPCPort.CalendarActionProvider.Stub;

namespace Calendar.ActionProvider;

public sealed class CalendarService : TizenActionCalendar.ServiceBase
{
    private readonly CalendarEventRepository _repository;
    private readonly CalendarCommandService? _commands;

    public CalendarService()
        : this(CalendarProviderState.Repository, CalendarProviderState.Commands)
    {
    }

    public CalendarService(CalendarEventRepository repository)
        : this(repository, commands: null)
    {
    }

    public CalendarService(CalendarEventRepository repository, CalendarCommandService? commands)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _commands = commands;
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus AddEvent(TizenEntityCalendarEvent calendar)
    {
        if (_commands is null)
        {
            return Failure("Calendar mutation service is unavailable.");
        }

        return TryToDomain(calendar, out var calendarEvent, out var failure)
            ? ToStatus(_commands.CreateEvent(calendarEvent!, []))
            : Failure(failure);
    }

    public override TizenEntityStatus DeleteEvent(TizenEntityCalendarEvent calendar)
    {
        if (_commands is null)
        {
            return Failure("Calendar mutation service is unavailable.");
        }

        return calendar is null || string.IsNullOrWhiteSpace(calendar.Id) || calendar.Id.Length > 256
            ? Failure("A stable event ID is required.")
            : ToStatus(_commands.DeleteEvent(calendar.Id));
    }

    public override TizenEntityStatus Search(TizenEntityCalendarQuery query, out List<TizenEntityCalendarEvent> result)
    {
        result = [];
        if (query is null) return Failure("A calendar query is required.");
        if (!CalendarSearchQueryAdapter.TryCreate(query.Keyword, query.StartDate, query.EndDate,
                query.Limit, true, true, true, out var criteria, out var error, query.Id, query.Category)) return Failure(error);
        result = _repository.Search(criteria!).Select(ToEntity).ToList();
        return Success();
    }

    public override TizenEntityStatus ToPresentation(List<TizenEntityCalendarEvent> calendarEvents, out TizenEntityPresentation result)
    {
        result = new() { Template = string.Empty, Document = string.Empty };
        if (calendarEvents is null || calendarEvents.Count > 100)
            return Failure("At most 100 calendar events are allowed.");
        var events = new List<CalendarEvent>();
        foreach (var entity in calendarEvents)
        {
            if (!TryToDomain(entity, out var calendarEvent, out var failure)) return Failure(failure);
            events.Add(calendarEvent!);
        }
        var presentation = CalendarA2UiPresentations.Create(events);
        if (!presentation.FitsTransport) return Failure("Presentation Template and Document must each fit within 64 Ki characters.");
        result.Template = presentation.Template;
        result.Document = presentation.Document;
        return Success();
    }

    public override TizenEntityStatus UpdateEvent(TizenEntityCalendarEvent calendar)
    {
        if (_commands is null)
        {
            return Failure("Calendar mutation service is unavailable.");
        }

        return TryToDomain(calendar, out var calendarEvent, out var failure)
            ? ToStatus(_commands.UpdateEvent(calendarEvent!, []))
            : Failure(failure);
    }

    private static bool TryToDomain(
        TizenEntityCalendarEvent? entity,
        out CalendarEvent? calendarEvent,
        out string failure)
    {
        calendarEvent = null;
        if (entity is null ||
            string.IsNullOrWhiteSpace(entity.Id) ||
            entity.Id.Length > 256 ||
            string.IsNullOrWhiteSpace(entity.Title) ||
            entity.Title.Length > 512 || entity.Note?.Length > 4096 || entity.Location?.Length > 512 ||
            entity.StartDate?.Length > 64 || entity.EndDate?.Length > 64 ||
            !DateTimeOffset.TryParse(entity.StartDate, out var start) ||
            !DateTimeOffset.TryParse(entity.EndDate, out var end) ||
            end <= start)
        {
            failure = "Calendar requires a stable ID (256 max), title (512 max), notes (4096 max), location (512 max), and a valid positive start/end range.";
            return false;
        }

        try
        {
            calendarEvent = CalendarEvent.Create(
                entity.Id,
                entity.Title,
                start,
                end,
                entity.Note ?? string.Empty,
                entity.Location ?? string.Empty);
            failure = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static TizenEntityStatus ToStatus(CalendarCommandResult result) =>
        result.Success ? Success() : Failure(result.Reason);

    private static TizenEntityCalendarEvent ToEntity(CalendarEvent calendarEvent) => new()
    {
        Id = calendarEvent.Id,
        Extra = string.Empty,
        Title = calendarEvent.Title,
        StartDate = calendarEvent.Start.ToString("O"),
        EndDate = calendarEvent.End.ToString("O"),
        Note = calendarEvent.Note,
        Location = calendarEvent.Location,
    };

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
