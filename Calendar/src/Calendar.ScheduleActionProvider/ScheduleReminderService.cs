#nullable enable

using Calendar.Domain;
using Calendar.UseCases;
using RPCPort.ScheduleReminderActionProvider;
using RPCPort.ScheduleReminderActionProvider.Stub;

namespace Calendar.ScheduleActionProvider;

public sealed class ScheduleReminderService : TizenActionSchedule.ServiceBase
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

    public override TizenEntityStatus CreateReminder(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Schedule reminder mutation service is unavailable.");
        }

        return TryToDomain(reminder, out var domainReminder, out var reason)
            ? ToStatus(_commands.CreateReminder(domainReminder!))
            : Failure(reason);
    }

    public override TizenEntityStatus UpdateReminder(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Schedule reminder mutation service is unavailable.");
        }

        return TryToDomain(reminder, out var domainReminder, out var reason)
            ? ToStatus(_commands.UpdateReminder(domainReminder!))
            : Failure(reason);
    }

    public override TizenEntityStatus DeleteReminder(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Schedule reminder mutation service is unavailable.");
        }

        return reminder is null || string.IsNullOrWhiteSpace(reminder.Id)
            ? Failure("A stable reminder ID is required.")
            : ToStatus(_commands.DeleteReminder(reminder.Id));
    }

    public override TizenEntityStatus CompleteReminder(TizenEntityReminder reminder)
    {
        if (_commands is null)
        {
            return Failure("Schedule reminder mutation service is unavailable.");
        }

        return reminder is null || string.IsNullOrWhiteSpace(reminder.Id)
            ? Failure("A stable reminder ID is required.")
            : ToStatus(_commands.SetReminderCompleted(reminder.Id, isCompleted: true));
    }

    public override TizenEntityStatus SearchReminder(TizenEntityQuery query, out List<TizenEntityReminder> result)
    {
        if (query is null)
        {
            result = [];
            return Failure("A query is required.");
        }

        var limit = query.Number <= 0 ? 20 : Math.Min(query.Number, 100);
        result = _reminders.Search(query.Keyword)
            .Where(reminder => reminder.CalendarEventId is null)
            .Take(limit)
            .Select(ToEntity)
            .ToList();
        return Success();
    }

    public override TizenEntityStatus AddRecording(TizenEntityReservation reservation) => Unsupported();

    public override TizenEntityStatus AddViewing(TizenEntityReservation reservation) => Unsupported();

    public override TizenEntityStatus CancelRecording(TizenEntityReservation reservation) => Unsupported();

    public override TizenEntityStatus CancelViewing(TizenEntityReservation reservation) => Unsupported();

    public override TizenEntityStatus GetReservations(out List<TizenEntityReservation> result)
    {
        result = [];
        return Unsupported();
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
            !DateTimeOffset.TryParse(entity.DueDate, out var dueAt))
        {
            reason = "Reminder requires a stable ID, title, and valid due date.";
            return false;
        }

        try
        {
            reminder = CalendarReminder.Create(entity.Id, entity.Title, dueAt, entity.Note) with
            {
                IsCompleted = entity.Completed,
            };
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
        Completed = reminder.IsCompleted,
    };

    private static TizenEntityStatus ToStatus(CalendarCommandResult result) =>
        result.Success ? Success() : Failure(result.Reason);

    private static TizenEntityStatus Unsupported() =>
        Failure("Recording and viewing reservations are not supported by the Calendar reminder provider.");

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
