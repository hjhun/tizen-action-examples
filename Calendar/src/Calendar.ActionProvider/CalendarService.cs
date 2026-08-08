#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using System.Text.Json;
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

    public override TizenEntityStatus GetEventByIds(
        List<string> ids,
        out List<TizenEntityCalendar> result,
        out List<string> unresolvedIds)
    {
        if (ids is null || ids.Count > 100 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256))
        {
            result = [];
            unresolvedIds = [];
            return Failure("ids must contain at most 100 non-empty stable IDs, each no longer than 256 characters.");
        }

        var resolution = _repository.ResolveByIds(ids);
        result = resolution.Events.Select(ToEntity).ToList();
        unresolvedIds = resolution.UnresolvedIds.ToList();
        return Success();
    }

    public override TizenEntityStatus AddEvent(TizenEntityCalendar calendar)
    {
        if (_commands is null)
        {
            return Failure("Calendar mutation service is unavailable.");
        }

        return TryToDomain(calendar, out var calendarEvent, out var failure)
            ? ToStatus(_commands.CreateEvent(calendarEvent!, []))
            : Failure(failure);
    }

    public override TizenEntityStatus RemoveEvent(TizenEntityCalendar calendar)
    {
        if (_commands is null)
        {
            return Failure("Calendar mutation service is unavailable.");
        }

        return calendar is null || string.IsNullOrWhiteSpace(calendar.Id)
            ? Failure("A stable event ID is required.")
            : ToStatus(_commands.DeleteEvent(calendar.Id));
    }

    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityCalendar> result)
    {
        if (query is null)
        {
            result = [];
            return Failure("A query is required.");
        }

        var limit = query.Number <= 0 ? 20 : Math.Min(query.Number, 100);
        result = _repository.Search(query.Keyword).Take(limit).Select(ToEntity).ToList();
        return Success();
    }

    public override TizenEntityStatus SearchInPeriod(
        TizenEntityCalendarSearchQuery calendarSearchQuery,
        out List<TizenEntityCalendar> result)
    {
        result = [];
        if (calendarSearchQuery is null)
        {
            return Failure("A calendar search query is required.");
        }

        if (!CalendarSearchQueryAdapter.TryCreate(
                calendarSearchQuery.Keyword,
                calendarSearchQuery.StartDate,
                calendarSearchQuery.EndDate,
                calendarSearchQuery.Number,
                calendarSearchQuery.SearchTitle,
                calendarSearchQuery.SearchLocation,
                calendarSearchQuery.SearchNote,
                out var criteria,
                out var error))
        {
            return Failure(error);
        }

        result = _repository.Search(criteria!).Select(ToEntity).ToList();
        return Success();
    }

    public override TizenEntityStatus ToPresentation(TizenEntityCalendar calendar, out TizenEntityPresentation result)
    {
        if (!TryToDomain(calendar, out var calendarEvent, out var failure))
        {
            result = new TizenEntityPresentation();
            return Failure(failure);
        }

        result = new TizenEntityPresentation
        {
            Template = "calendar-event-card-v1",
            Document = JsonSerializer.Serialize(new
            {
                id = calendarEvent!.Id,
                title = calendarEvent.Title,
                start = calendarEvent.Start,
                end = calendarEvent.End,
                note = calendarEvent.Note,
                location = calendarEvent.Location,
            }),
        };
        return Success();
    }

    public override TizenEntityStatus UpdateEvent(TizenEntityCalendar calendar)
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
        TizenEntityCalendar? entity,
        out CalendarEvent? calendarEvent,
        out string failure)
    {
        calendarEvent = null;
        if (entity is null ||
            string.IsNullOrWhiteSpace(entity.Id) ||
            entity.Id.Length > 256 ||
            string.IsNullOrWhiteSpace(entity.Title) ||
            !DateTimeOffset.TryParse(entity.StartDate, out var start) ||
            !DateTimeOffset.TryParse(entity.EndDate, out var end) ||
            end <= start)
        {
            failure = "Calendar requires a stable ID, title, and a valid positive start/end range.";
            return false;
        }

        try
        {
            calendarEvent = CalendarEvent.Create(
                entity.Id,
                entity.Title,
                start,
                end,
                entity.Note,
                entity.Location);
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

    private static TizenEntityCalendar ToEntity(CalendarEvent calendarEvent) => new()
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
