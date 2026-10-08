#nullable enable
using Reminder.Domain;
using Reminder.UseCases;
using RPCPort.ReminderScheduleActionProvider;
using RPCPort.ReminderScheduleActionProvider.Stub;

namespace Reminder.ScheduleActionProvider;

public sealed class ReminderScheduleService : TizenActionReminder.ServiceBase
{
    private readonly ScheduleService _service;
    public ReminderScheduleService() : this(ProviderState.Service ?? throw new InvalidOperationException("Reminder provider is not configured.")) { }
    public ReminderScheduleService(ScheduleService service) => _service = service;
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus Add(TizenEntityReminder reminder, out TizenEntityReminder result)
    {
        result = null!;
        return Failure("unavailable: canonical Add is not implemented");
    }
    public override TizenEntityStatus Update(TizenEntityReminder reminder, out TizenEntityReminder result)
    {
        result = new() { Title = string.Empty, State = new() { State = "To-do" } };
        return Failure("unavailable: canonical Update is not implemented");
    }
    public override TizenEntityStatus Delete(TizenEntityReminder reminder) => reminder is null
        ? Failure("invalid: reminder is required") : ToStatus(_service.DeleteReminder(reminder.Id));

    public override TizenEntityStatus Search(TizenEntityReminderQuery query, out List<TizenEntityReminder> result)
    {
        result = [];
        if (query is null) return Failure("invalid: query is required");
        if (HasUnsupportedFilters(query))
            return Failure("unavailable: reminder filters are not implemented");
        if (query.State is not null && query.State.State is not ("To-do" or "Done"))
            return Failure("invalid: State must be To-do or Done");
        try
        {
            var criteria = ReminderContract.Query(query.Id, query.Keyword, query.Category, query.Limit ?? 0);
            var snapshot = _service.Snapshot.Reminders;
            var keyword = criteria.Keyword.Trim();
            var matched = snapshot
                .Where(item => string.IsNullOrWhiteSpace(criteria.Id) || item.Id == criteria.Id)
                .Where(item => keyword.Length == 0 || item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.Note.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Where(item => query.State is null ? !item.Completed : item.State == query.State.State)
                .ToArray();
            if (matched.Any(item => item.State is not ("To-do" or "Done")))
                return Failure("unavailable: matched reminder state cannot be represented");
            result = matched.OrderBy(item => item.Completed)
                .ThenBy(item => item.Completed
                    ? -(item.CompletedAt ?? item.CreatedAt).UtcTicks
                    : (item.DueAt ?? DateTimeOffset.MaxValue).UtcTicks)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Take(criteria.Limit).Select(ToEntity).ToList();
            return Success();
        }
        catch (ArgumentException exception) { return Failure("invalid: " + exception.Message); }
    }

    private static TizenEntityStatus Mutate(TizenEntityReminder entity, Func<ReminderItem, CommandResult> command)
    {
        if (entity is null) return Failure("invalid: reminder is required");
        try { return ToStatus(command(ReminderContract.Item(entity.Id, entity.Title, entity.DueDate, entity.Note, entity.State?.State))); }
        catch (ArgumentException exception) { return Failure("invalid: " + exception.Message); }
    }

    private static bool HasUnsupportedFilters(TizenEntityReminderQuery query) =>
        query.StartDate is not null || query.EndDate is not null ||
        query.CategoryId is not null || query.SourceAppId is not null ||
        query.ExternalId is not null || query.Profiles is not null;

    internal static TizenEntityReminder ToEntity(ReminderItem item) => new()
    {
        Id = item.Id, Extra = string.Empty, Title = item.Title, Note = item.Note,
        DueDate = item.DueAt?.ToString("O"),
        State = new TizenEntityReminderState { Id = string.Empty, Extra = string.Empty, State = item.State },
    };
    private static TizenEntityStatus ToStatus(CommandResult result) => new() { Success = result.Success, Reason = result.Reason };
    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
