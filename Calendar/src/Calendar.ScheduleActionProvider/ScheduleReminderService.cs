#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using RPCPort.ScheduleReminderActionProvider;
using RPCPort.ScheduleReminderActionProvider.Stub;

namespace Calendar.ScheduleActionProvider;

public sealed class ScheduleReminderService : TizenActionReminder.ServiceBase
{
    private readonly CalendarReminderRepository _reminders;
    private readonly CalendarCommandService? _commands;

    public ScheduleReminderService()
        : this(ScheduleProviderState.Reminders, ScheduleProviderState.Commands)
    {
    }

    public ScheduleReminderService(CalendarReminderRepository reminders, CalendarCommandService? commands)
    {
        _reminders = reminders ?? throw new ArgumentNullException(nameof(reminders));
        _commands = commands;
    }

    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus Add(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Reminder mutation service is unavailable.");
        }

        return TryToDomain(reminder, out var domainReminder, out var reason)
            ? ToStatus(_commands.CreateReminder(domainReminder!))
            : Failure(reason);
    }

    public override TizenEntityStatus Update(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Reminder mutation service is unavailable.");
        }

        return TryToDomain(reminder, out var domainReminder, out var reason)
            ? ToStatus(_commands.UpdateReminder(domainReminder!))
            : Failure(reason);
    }

    public override TizenEntityStatus Delete(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Reminder mutation service is unavailable.");
        }

        return reminder is null || string.IsNullOrWhiteSpace(reminder.Id) || reminder.Id.Length > 256
            ? Failure("A stable reminder ID is required.")
            : ToStatus(_commands.DeleteReminder(reminder.Id));
    }

    public override TizenEntityStatus Search(TizenEntityQuery query, out List<TizenEntityReminder> result)
    {
        if (query is null || query.Keyword?.Length > 512)
        {
            result = [];
            return Failure("A query with at most 512 keyword characters is required.");
        }

        if (query.Id?.Length > 256 || query.Category?.Length > 256)
        {
            result = [];
            return Failure("Query Id and Category must not exceed 256 characters.");
        }
        if (!string.IsNullOrWhiteSpace(query.Category) &&
            query.Category is not ("Reminder" or "Tizen.Action.Reminder" or "org.tizen.actionexamples.calendar"))
        {
            result = [];
            return Failure("Category must name Reminder, Tizen.Action.Reminder, or this app.");
        }
        var limit = query.Limit <= 0 ? 20 : Math.Min(query.Limit, 100);
        result = _reminders.Search(query.Keyword)
            .Where(reminder => reminder.CalendarEventId is null)
            .Where(reminder => string.IsNullOrWhiteSpace(query.Id) || reminder.Id == query.Id)
            .Take(limit)
            .Select(ToEntity)
            .ToList();
        return Success();
    }

    public override TizenEntityStatus ToPresentation(TizenEntityReminder entity, out TizenEntityPresentation result)
    {
        result = new() { Template = string.Empty, Document = string.Empty };
        if (!TryToDomain(entity, out var reminder, out var reason)) return Failure(reason);
        var presentation = CalendarA2UiPresentations.CreateReminder(reminder!);
        result.Template = presentation.Template;
        result.Document = presentation.Document;
        return Success();
    }

    private static bool TryToDomain(
        TizenEntityReminder? entity,
        out CalendarReminder? reminder,
        out string reason)
    {
        reminder = null;
        if (entity is null ||
            string.IsNullOrWhiteSpace(entity.Id) ||
            entity.Id.Length > 256 ||
            string.IsNullOrWhiteSpace(entity.Title) ||
            entity.Title.Length > 512 || entity.Note?.Length > 4096 || entity.DueDate?.Length > 64 ||
            !DateTimeOffset.TryParse(entity.DueDate, out var dueAt))
        {
            reason = "Reminder requires a stable ID (256 max), title (512 max), notes (4096 max), and valid due date.";
            return false;
        }

        try
        {
            reminder = CalendarReminder.Create(entity.Id, entity.Title, dueAt, entity.Note).WithState(entity.State?.State);
            reason = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    private static TizenEntityReminder ToEntity(CalendarReminder reminder) => new()
    {
        Id = reminder.Id,
        Extra = string.Empty,
        Title = reminder.Title,
        DueDate = reminder.DueAt.ToString("O"),
        Note = reminder.Note,
        State = new TizenEntityReminderState { Id = string.Empty, Extra = string.Empty, State = reminder.State },
    };

    private static TizenEntityStatus ToStatus(CalendarCommandResult result) =>
        result.Success ? Success() : Failure(result.Reason);

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
