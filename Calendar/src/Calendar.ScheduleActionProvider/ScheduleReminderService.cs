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

    public override TizenEntityStatus Search(TizenEntityReminderQuery query, out List<TizenEntityReminder> result)
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
            query.Category is not ("Reminder" or "Tizen.Action.Reminder" or "org.tizen.calendar"))
        {
            result = [];
            return Failure("Category must name Reminder, Tizen.Action.Reminder, or this app.");
        }
        if (HasUnsupportedFilters(query))
        {
            result = [];
            return Failure("unavailable: reminder filters are not implemented");
        }
        if (query.State is not null && query.State.State is not ("To-do" or "Done"))
        {
            result = [];
            return Failure("invalid: State must be To-do or Done");
        }
        var requestedLimit = query.Limit ?? 0;
        var limit = requestedLimit <= 0 ? 20 : Math.Min(requestedLimit, 100);
        var matched = _reminders.Search(query.Keyword)
            .Where(reminder => reminder.CalendarEventId is null)
            .Where(reminder => string.IsNullOrWhiteSpace(query.Id) || reminder.Id == query.Id)
            .Where(reminder => query.State is null ? !reminder.IsCompleted : reminder.State == query.State.State)
            .ToArray();
        if (matched.Any(reminder => reminder.State is not ("To-do" or "Done")))
        {
            result = [];
            return Failure("unavailable: matched reminder state cannot be represented");
        }
        result = matched.Take(limit).Select(ToEntity).ToList();
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

    private static bool HasUnsupportedFilters(TizenEntityReminderQuery query) =>
        query.StartDate is not null || query.EndDate is not null ||
        query.CategoryId is not null || query.SourceAppId is not null ||
        query.ExternalId is not null || query.Profiles is not null;

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
