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

    public override TizenEntityStatus Add(TizenEntityReminder reminder) => Mutate(reminder, _service.CreateReminder);
    public override TizenEntityStatus Update(TizenEntityReminder reminder) => Mutate(reminder, _service.UpdateReminder);
    public override TizenEntityStatus Delete(TizenEntityReminder reminder) => reminder is null
        ? Failure("invalid: reminder is required") : ToStatus(_service.DeleteReminder(reminder.Id));

    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityReminder> result)
    {
        result = [];
        if (query is null) return Failure("invalid: query is required");
        try
        {
            result = _service.SearchReminders(ReminderContract.Query(query.Id, query.Keyword, query.Category, query.Limit))
                .Select(ToEntity).ToList();
            return Success();
        }
        catch (ArgumentException exception) { return Failure("invalid: " + exception.Message); }
    }

    public override TizenEntityStatus ToPresentation(TizenEntityReminder reminder, out TizenEntityPresentation result)
    {
        result = new() { Template = string.Empty, Document = string.Empty };
        try
        {
            if (reminder is null) return Failure("invalid: reminder is required");
            // Present the current persisted entity, never stale caller fields.
            var current = _service.ResolveReminderIds([reminder.Id]).Items.FirstOrDefault();
            if (current is null) return Failure("not_found: reminder does not exist");
            var presentation = ReminderPresentation.Create(current);
            result.Template = presentation.Template;
            result.Document = presentation.Document;
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

    internal static TizenEntityReminder ToEntity(ReminderItem item) => new()
    {
        Id = item.Id, Extra = string.Empty, Title = item.Title, Note = item.Note,
        DueDate = item.DueAt?.ToString("O") ?? string.Empty,
        State = new TizenEntityReminderState { Id = string.Empty, Extra = string.Empty, State = item.State },
    };
    private static TizenEntityStatus ToStatus(CommandResult result) => new() { Success = result.Success, Reason = result.Reason };
    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
