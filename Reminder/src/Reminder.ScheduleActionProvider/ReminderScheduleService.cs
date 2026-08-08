#nullable enable
using System.Globalization;
using Reminder.Domain;
using Reminder.UseCases;
using RPCPort.ReminderScheduleActionProvider;
using RPCPort.ReminderScheduleActionProvider.Stub;

namespace Reminder.ScheduleActionProvider;

public sealed class ReminderScheduleService : TizenActionSchedule.ServiceBase
{
    private readonly ScheduleService _service;

    public ReminderScheduleService() : this(ProviderState.Service ?? throw new InvalidOperationException("Schedule provider is not configured.")) { }
    public ReminderScheduleService(ScheduleService service) => _service = service;
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus AddRecording(TizenEntityReservation reservation) => AddReservation(reservation, ReservationKind.Recording);
    public override TizenEntityStatus AddViewing(TizenEntityReservation reservation) => AddReservation(reservation, ReservationKind.Viewing);
    public override TizenEntityStatus CancelRecording(TizenEntityReservation reservation) => CancelReservation(reservation, ReservationKind.Recording);
    public override TizenEntityStatus CancelViewing(TizenEntityReservation reservation) => CancelReservation(reservation, ReservationKind.Viewing);

    public override TizenEntityStatus CompleteReminder(TizenEntityReminder reminder) =>
        reminder is null ? Failure("invalid: reminder is required") : ToStatus(_service.CompleteReminder(reminder.Id));

    public override TizenEntityStatus CreateReminder(TizenEntityReminder reminder) =>
        TryReminder(reminder, out var item, out var reason)
            ? ToStatus(_service.CreateReminder(item!))
            : Failure(reason);

    public override TizenEntityStatus UpdateReminder(TizenEntityReminder reminder) =>
        TryReminder(reminder, out var item, out var reason)
            ? ToStatus(_service.UpdateReminder(item!))
            : Failure(reason);

    public override TizenEntityStatus DeleteReminder(TizenEntityReminder reminder) =>
        reminder is null ? Failure("invalid: reminder is required") : ToStatus(_service.DeleteReminder(reminder.Id));

    public override TizenEntityStatus GetReservations(out List<TizenEntityReservation> result)
    {
        result = _service.GetReservations().Select(ToEntity).ToList();
        return Success("Common-simulated: deterministic reservation catalog");
    }

    public override TizenEntityStatus SearchReminder(TizenEntityQuery query, out List<TizenEntityReminder> result)
    {
        result = [];
        if (query is null) return Failure("invalid: query is required");
        if (!TryCategory(query.Category, out var category)) return Failure("invalid: unsupported reminder category");
        var limit = query.Number == 0 ? 50 : query.Number;
        try
        {
            result = _service.SearchReminders(new ReminderQuery(query.Keyword ?? string.Empty, category, limit)).Select(ToEntity).ToList();
            return Success();
        }
        catch (ArgumentException exception) { return Failure("invalid: " + exception.Message); }
    }

    private TizenEntityStatus AddReservation(TizenEntityReservation entity, ReservationKind kind)
    {
        return TryReservation(entity, kind, out var item, out var reason)
            ? ToStatus(_service.AddReservation(item!, kind))
            : Failure(reason);
    }

    private TizenEntityStatus CancelReservation(TizenEntityReservation entity, ReservationKind kind) =>
        entity is null ? Failure("invalid: reservation is required") : ToStatus(_service.CancelReservation(entity.Id, kind));

    private static bool TryReminder(TizenEntityReminder? entity, out ReminderItem? item, out string reason)
    {
        item = null;
        if (entity is null) { reason = "invalid: reminder is required"; return false; }
        if (!TryDate(entity.DueDate, optional: true, out var due)) { reason = "invalid: DueDate must be RFC 3339 with an offset"; return false; }
        try
        {
            item = ReminderItem.Create(entity.Id, entity.Title, due, entity.Note) with { Completed = entity.Completed };
            reason = string.Empty;
            return true;
        }
        catch (ArgumentException exception) { reason = "invalid: " + exception.Message; return false; }
    }

    private static bool TryReservation(TizenEntityReservation? entity, ReservationKind expectedKind, out ReservationItem? item, out string reason)
    {
        item = null;
        if (entity is null || !TryDate(entity.StartTime, optional: false, out var start) || !TryDate(entity.EndTime, optional: false, out var end))
        { reason = "invalid: reservation and RFC 3339 start/end times are required"; return false; }
        if (!string.IsNullOrWhiteSpace(entity.Kind) && !string.Equals(entity.Kind, expectedKind.ToString(), StringComparison.OrdinalIgnoreCase))
        { reason = "invalid: reservation kind conflicts with Action"; return false; }
        if (!TryRepeat(entity.Repeat, out var repeat)) { reason = "invalid: Repeat must be once, daily, weekly, or weekdays"; return false; }
        try
        {
            var channel = entity.Channel?.Name ?? entity.Channel?.Id ?? string.Empty;
            var program = entity.Program?.Title ?? entity.Program?.Id ?? string.Empty;
            item = ReservationItem.Create(entity.Id, expectedKind, channel, program, start!.Value, end!.Value, repeat);
            reason = string.Empty;
            return true;
        }
        catch (ArgumentException exception) { reason = "invalid: " + exception.Message; return false; }
    }

    private static bool TryDate(string? text, bool optional, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return optional;
        var hasOffset = text.EndsWith('Z') || (text.Length >= 6 && (text[^6] == '+' || text[^6] == '-') && text[^3] == ':');
        if (!hasOffset || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) return false;
        value = parsed;
        return true;
    }

    private static bool TryCategory(string? text, out ReminderCategory category)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? "upcoming" : text.Trim().ToLowerInvariant();
        category = normalized switch
        {
            "today" => ReminderCategory.Today,
            "upcoming" => ReminderCategory.Upcoming,
            "overdue" => ReminderCategory.Overdue,
            "completed" => ReminderCategory.Completed,
            "no-alert" => ReminderCategory.NoAlert,
            "all" => ReminderCategory.All,
            _ => (ReminderCategory)(-1),
        };
        return (int)category >= 0;
    }

    private static bool TryRepeat(string? text, out ReservationRepeat repeat) => Enum.TryParse(string.IsNullOrWhiteSpace(text) ? "Once" : text, true, out repeat);

    private static TizenEntityReminder ToEntity(ReminderItem item) => new()
    {
        Id = item.Id, Extra = string.Empty, Title = item.Title, DueDate = item.DueAt?.ToString("O") ?? string.Empty,
        Note = item.Note, Completed = item.Completed,
    };

    private static TizenEntityReservation ToEntity(ReservationItem item)
    {
        var channel = new TizenEntityChannel
        {
            Id = item.Channel,
            Extra = string.Empty,
            Major = 0,
            Minor = 0,
            Name = item.Channel,
            ServiceId = 0,
            Source = string.Empty,
            Signal = string.Empty,
        };
        var program = new TizenEntityProgram
        {
            Id = item.Program,
            Extra = string.Empty,
            Title = item.Program,
            Channel = channel,
            StartTime = item.StartAt.ToString("O"),
            EndTime = item.EndAt.ToString("O"),
            Genre = string.Empty,
            Rating = string.Empty,
            Description = string.Empty,
            IsLive = false,
        };
        return new TizenEntityReservation
        {
            Id = item.Id,
            Extra = string.Empty,
            Channel = channel,
            Program = program,
            StartTime = item.StartAt.ToString("O"),
            EndTime = item.EndAt.ToString("O"),
            Repeat = item.Repeat.ToString().ToLowerInvariant(),
            Kind = item.Kind.ToString().ToLowerInvariant(),
        };
    }

    private static TizenEntityStatus ToStatus(CommandResult result) => new() { Success = result.Success, Reason = result.Reason };
    private static TizenEntityStatus Success(string reason = "") => new() { Success = true, Reason = reason };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
