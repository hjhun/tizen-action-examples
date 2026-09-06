#nullable enable
using System.Text.Json;
using Calendar.Domain;
using ActionExamples.ViewAnnotations;
using RPCPort.CalendarViewActionProvider;
using CalendarEntity = RPCPort.CalendarActionProvider.TizenEntityCalendarEvent;
using ReminderEntity = RPCPort.ScheduleReminderActionProvider.TizenEntityReminder;
using ReminderState = RPCPort.ScheduleReminderActionProvider.TizenEntityReminderState;

namespace Calendar.ViewActionProvider;

public static class CalendarViewSnapshots
{
    public static CurrentViewSnapshot Context(string id, string type, string title, object state) => new(
        id, type, title, "Tizen.Entity", id,
        new TizenEntity { Id = id, Extra = JsonSerializer.Serialize(state) }.ToJson());

    public static CurrentViewSnapshot Event(string viewId, CalendarEvent item, bool includeNote) => new(
        viewId, "Calendar.Event", item.Title, "Tizen.Entity.CalendarEvent", item.Id,
        new CalendarEntity
        {
            Id = item.Id, Extra = string.Empty, Title = item.Title, StartDate = item.Start.ToString("O"),
            EndDate = item.End.ToString("O"), Location = item.Location, Note = includeNote ? item.Note : string.Empty,
        }.ToJson());

    public static CurrentViewSnapshot Reminder(string viewId, CalendarReminder item, bool includeNote) => new(
        viewId, "Calendar.Reminder", item.Title, "Tizen.Entity.Reminder", item.Id,
        new ReminderEntity
        {
            Id = item.Id, Extra = string.Empty, Title = item.Title, DueDate = item.DueAt.ToString("O"),
            Note = includeNote ? item.Note : string.Empty, State = new ReminderState { Id = string.Empty, Extra = string.Empty, State = item.State },
        }.ToJson());
}
