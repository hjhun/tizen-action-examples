#nullable enable
using System.Globalization;
using Reminder.Domain;
using Reminder.UseCases;
using RPCPort.ReminderCustomActionProvider;
using RPCPort.ReminderCustomActionProvider.Stub;

namespace Reminder.ScheduleActionProvider;

public sealed class ReminderCustomService : TizenActionReminderCustom.ServiceBase
{
    private readonly ScheduleService _service;

    public ReminderCustomService() : this(ProviderState.Service ?? throw new InvalidOperationException("Schedule provider is not configured.")) { }
    public ReminderCustomService(ScheduleService service) => _service = service;
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus AddRecording(TizenEntityReservation reservation) => AddReservation(reservation, ReservationKind.Recording);
    public override TizenEntityStatus AddViewing(TizenEntityReservation reservation) => AddReservation(reservation, ReservationKind.Viewing);
    public override TizenEntityStatus CancelRecording(TizenEntityReservation reservation) => CancelReservation(reservation, ReservationKind.Recording);
    public override TizenEntityStatus CancelViewing(TizenEntityReservation reservation) => CancelReservation(reservation, ReservationKind.Viewing);

    public override TizenEntityStatus GetReservations(out List<TizenEntityReservation> result)
    {
        result = _service.GetReservations().Select(ToEntity).ToList();
        return Success("Common-simulated: deterministic reservation catalog");
    }

    public override TizenEntityStatus GetReminderByIds(List<string> ids, out List<TizenEntityReminder> result, out List<string> unresolvedIds)
    {
        result = []; unresolvedIds = [];
        try
        {
            var resolved = _service.ResolveReminderIds(ids);
            result = resolved.Items.Select(ToEntity).ToList();
            unresolvedIds = resolved.UnresolvedIds.ToList();
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

    private static bool TryReservation(TizenEntityReservation? entity, ReservationKind expectedKind, out ReservationItem? item, out string reason)
    {
        item = null;
        if (entity is null || !TryDate(entity.StartTime, optional: false, out var start) || !TryDate(entity.EndTime, optional: false, out var end))
        { reason = "invalid: reservation and RFC 3339 start/end times are required"; return false; }
        if (!string.IsNullOrWhiteSpace(entity.Kind) && !string.Equals(entity.Kind, expectedKind.ToString(), StringComparison.OrdinalIgnoreCase))
        { reason = "invalid: reservation kind conflicts with Action"; return false; }
        if (!TryRepeat(entity.Repeat, out var repeat)) { reason = "invalid: Repeat must be once, daily, or weekly"; return false; }
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

    private static bool TryRepeat(string? text, out ReservationRepeat repeat) =>
        Enum.TryParse(string.IsNullOrWhiteSpace(text) ? "Once" : text, true, out repeat) &&
        Enum.IsDefined(repeat) && repeat != ReservationRepeat.Weekdays;

    private static TizenEntityReminder ToEntity(ReminderItem item) => new()
    {
        Id = item.Id, Extra = string.Empty, Title = item.Title, DueDate = item.DueAt?.ToString("O") ?? string.Empty,
        Note = item.Note, State = new TizenEntityReminderState { Id = string.Empty, Extra = string.Empty, State = item.State },
    };

    internal static TizenEntityReservation ToEntity(ReservationItem item)
    {
        var channel = new TizenEntityChannel
        {
            Id = item.Channel, Extra = string.Empty, Name = item.Channel,
            ServiceId = string.Empty, ServiceProvider = string.Empty,
        };
        return new()
        {
            Id = item.Id, Extra = string.Empty, Channel = channel,
            Program = new TizenEntityProgram
            {
                Id = item.Program, Extra = string.Empty, Title = item.Program, Channel = channel,
                StartTime = item.StartAt.ToString("O"), EndTime = item.EndAt.ToString("O"),
                Genre = string.Empty, AgeRating = string.Empty, Description = string.Empty,
            },
            StartTime = item.StartAt.ToString("O"), EndTime = item.EndAt.ToString("O"),
            Repeat = item.Repeat.ToString().ToLowerInvariant(), Kind = item.Kind.ToString().ToLowerInvariant(),
        };
    }

    private static TizenEntityStatus ToStatus(CommandResult result) => new() { Success = result.Success, Reason = result.Reason };
    private static TizenEntityStatus Success(string reason = "") => new() { Success = true, Reason = reason };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}
