#nullable enable
using System.Text.Json;
using Reminder.Domain;
using ActionExamples.ViewAnnotations;
using RPCPort.ReminderViewActionProvider;
using ScheduleReminder = RPCPort.ReminderScheduleActionProvider.TizenEntityReminder;
using ScheduleReservation = RPCPort.ReminderCustomActionProvider.TizenEntityReservation;
using ScheduleChannel = RPCPort.ReminderCustomActionProvider.TizenEntityChannel;
using ScheduleProgram = RPCPort.ReminderCustomActionProvider.TizenEntityProgram;

namespace Reminder.ViewActionProvider;

public static class ReminderViewSnapshots
{
    public static CurrentViewSnapshot Context(string id, string type, string title, object state) => new(
        id, type, title, "Tizen.Entity", id,
        new TizenEntity { Id = id, Extra = JsonSerializer.Serialize(state) }.ToJson());

    public static CurrentViewSnapshot Reminder(string viewId, ReminderItem item, bool includeNote) => new(
        viewId, "Reminder.Card", item.Title, "Tizen.Entity.Reminder", item.Id,
        new ScheduleReminder
        {
            Id = item.Id, Extra = string.Empty, Title = item.Title, DueDate = item.DueAt?.ToString("O") ?? string.Empty,
            Note = includeNote ? item.Note : string.Empty, State = new RPCPort.ReminderScheduleActionProvider.TizenEntityReminderState { Id = string.Empty, Extra = string.Empty, State = item.State },
        }.ToJson());

    public static CurrentViewSnapshot Reservation(string viewId, ReservationItem item) => new(
        viewId, "Reminder.ReservationCard", item.Program, "Tizen.Entity.Reservation", item.Id,
        new ScheduleReservation
        {
            Id = item.Id, Extra = string.Empty,
            Channel = new ScheduleChannel { Id = item.Channel, Extra = string.Empty, Name = item.Channel },
            Program = new ScheduleProgram { Id = item.Program, Extra = string.Empty, Title = item.Program },
            StartTime = item.StartAt.ToString("O"), EndTime = item.EndAt.ToString("O"),
            Repeat = item.Repeat.ToString().ToLowerInvariant(), Kind = item.Kind.ToString().ToLowerInvariant(),
        }.ToJson());
}
