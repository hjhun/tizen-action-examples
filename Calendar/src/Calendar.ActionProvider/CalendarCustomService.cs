#nullable enable
using Calendar.Domain;
using RPCPort.CalendarCustomActionProvider;
using RPCPort.CalendarCustomActionProvider.Stub;

namespace Calendar.ActionProvider;

public sealed class CalendarCustomService : TizenActionCalendarCustom.ServiceBase
{
    private readonly CalendarEventRepository _repository;
    public CalendarCustomService() : this(CalendarProviderState.Repository) { }
    public CalendarCustomService(CalendarEventRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus GetEventByIds(
        List<string> ids,
        out List<TizenEntityCalendarEvent> result,
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

    public override TizenEntityStatus SearchInPeriod(
        CalendarEntitySearchQuery calendarSearchQuery,
        out List<TizenEntityCalendarEvent> result)
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
                calendarSearchQuery.Limit,
                calendarSearchQuery.SearchTitle,
                calendarSearchQuery.SearchLocation,
                calendarSearchQuery.SearchNote,
                out var criteria,
                out var error, calendarSearchQuery.Id, calendarSearchQuery.Category))
        {
            return Failure(error);
        }

        result = _repository.Search(criteria!).Select(ToEntity).ToList();
        return Success();
    }

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
