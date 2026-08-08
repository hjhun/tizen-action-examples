using Calendar.Domain;
using Calendar.Persistence;

namespace Calendar.UseCases;

public interface ICalendarPersistence
{
    CalendarStoreDocument Load();
    void Save(CalendarStoreDocument document);
}

public interface IReminderAlarmScheduler
{
    int? Schedule(CalendarReminder reminder);
    void Cancel(int alarmId);
}

public sealed record CalendarCommandResult(bool Success, string Reason)
{
    public static CalendarCommandResult Succeeded() => new(true, string.Empty);
    public static CalendarCommandResult Failed(string reason) => new(false, reason);
}

public sealed class CalendarCommandService
{
    private readonly object _gate = new();
    private readonly CalendarEventRepository _events;
    private readonly CalendarReminderRepository _reminders;
    private readonly ICalendarPersistence _persistence;
    private readonly IReminderAlarmScheduler _alarms;

    public CalendarCommandService(
        CalendarEventRepository events,
        CalendarReminderRepository reminders,
        ICalendarPersistence persistence,
        IReminderAlarmScheduler alarms)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _reminders = reminders ?? throw new ArgumentNullException(nameof(reminders));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _alarms = alarms ?? throw new ArgumentNullException(nameof(alarms));
    }

    public CalendarCommandResult CreateEvent(CalendarEvent calendarEvent, IEnumerable<int> reminderOffsets)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(reminderOffsets);

        lock (_gate)
        {
            if (_events.ResolveByIds([calendarEvent.Id]).Events.Count != 0)
            {
                return CalendarCommandResult.Failed($"Event '{calendarEvent.Id}' already exists.");
            }

            IReadOnlyList<int> offsets;
            try
            {
                offsets = NormalizeOffsets(reminderOffsets);
            }
            catch (ArgumentException exception)
            {
                return CalendarCommandResult.Failed(exception.Message);
            }

            var eventSnapshot = _events.Snapshot();
            var reminderSnapshot = _reminders.Snapshot();
            var scheduledAlarmIds = new List<int>();
            var linkedReminders = new List<CalendarReminder>();

            try
            {
                foreach (var offset in offsets)
                {
                    var reminder = CalendarReminder.CreateForEvent(
                        id: LinkedReminderId(calendarEvent.Id, offset),
                        title: calendarEvent.Title,
                        eventStart: calendarEvent.Start,
                        calendarEventId: calendarEvent.Id,
                        offsetMinutes: offset,
                        note: calendarEvent.Note);
                    var alarmId = _alarms.Schedule(reminder);
                    if (alarmId is int value)
                    {
                        scheduledAlarmIds.Add(value);
                        reminder = reminder with { AlarmId = value };
                    }

                    linkedReminders.Add(reminder);
                }

                var desiredEvents = eventSnapshot.Append(calendarEvent).ToArray();
                var desiredReminders = reminderSnapshot.Concat(linkedReminders).ToArray();
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    desiredEvents,
                    desiredReminders));
                _events.ReplaceAll(desiredEvents);
                _reminders.ReplaceAll(desiredReminders);
                return CalendarCommandResult.Succeeded();
            }
            catch (Exception exception)
            {
                foreach (var alarmId in scheduledAlarmIds)
                {
                    TryCancel(alarmId);
                }

                return CalendarCommandResult.Failed(exception.Message);
            }
        }
    }

    public CalendarCommandResult Restore()
    {
        lock (_gate)
        {
            var scheduledAlarmIds = new List<int>();
            try
            {
                var document = _persistence.Load();
                var restoredReminders = new List<CalendarReminder>(document.Reminders.Count);
                foreach (var reminder in document.Reminders)
                {
                    if (reminder.AlarmId is int existingAlarmId)
                    {
                        TryCancel(existingAlarmId);
                    }

                    var restored = reminder with { AlarmId = null };
                    if (!restored.IsCompleted)
                    {
                        var alarmId = _alarms.Schedule(restored);
                        if (alarmId is int value)
                        {
                            scheduledAlarmIds.Add(value);
                            restored = restored with { AlarmId = value };
                        }
                    }

                    restoredReminders.Add(restored);
                }

                var reconciled = new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    document.Events,
                    restoredReminders);
                _persistence.Save(reconciled);
                _events.ReplaceAll(reconciled.Events);
                _reminders.ReplaceAll(reconciled.Reminders);
                return CalendarCommandResult.Succeeded();
            }
            catch (Exception exception)
            {
                foreach (var alarmId in scheduledAlarmIds)
                {
                    TryCancel(alarmId);
                }

                return CalendarCommandResult.Failed(exception.Message);
            }
        }
    }

    public CalendarCommandResult UpdateEvent(CalendarEvent calendarEvent, IEnumerable<int> reminderOffsets)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(reminderOffsets);

        lock (_gate)
        {
            if (_events.ResolveByIds([calendarEvent.Id]).Events.Count == 0)
            {
                return CalendarCommandResult.Failed($"Event '{calendarEvent.Id}' was not found.");
            }

            IReadOnlyList<int> offsets;
            try
            {
                offsets = NormalizeOffsets(reminderOffsets);
            }
            catch (ArgumentException exception)
            {
                return CalendarCommandResult.Failed(exception.Message);
            }

            var eventSnapshot = _events.Snapshot();
            var reminderSnapshot = _reminders.Snapshot();
            var oldLinkedReminders = reminderSnapshot
                .Where(reminder => string.Equals(reminder.CalendarEventId, calendarEvent.Id, StringComparison.Ordinal))
                .ToArray();
            var scheduledAlarmIds = new List<int>();
            var replacementReminders = new List<CalendarReminder>();

            try
            {
                foreach (var offset in offsets)
                {
                    var reminder = CalendarReminder.CreateForEvent(
                        LinkedReminderId(calendarEvent.Id, offset),
                        calendarEvent.Title,
                        calendarEvent.Start,
                        calendarEvent.Id,
                        offset,
                        calendarEvent.Note);
                    var alarmId = _alarms.Schedule(reminder);
                    if (alarmId is int value)
                    {
                        scheduledAlarmIds.Add(value);
                        reminder = reminder with { AlarmId = value };
                    }

                    replacementReminders.Add(reminder);
                }

                var desiredEvents = eventSnapshot
                    .Select(item => item.Id == calendarEvent.Id ? calendarEvent : item)
                    .ToArray();
                var desiredReminders = reminderSnapshot
                    .Where(reminder => !string.Equals(reminder.CalendarEventId, calendarEvent.Id, StringComparison.Ordinal))
                    .Concat(replacementReminders)
                    .ToArray();
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    desiredEvents,
                    desiredReminders));

                foreach (var oldReminder in oldLinkedReminders)
                {
                    if (oldReminder.AlarmId is int oldAlarmId)
                    {
                        TryCancel(oldAlarmId);
                    }
                }

                _events.ReplaceAll(desiredEvents);
                _reminders.ReplaceAll(desiredReminders);
                return CalendarCommandResult.Succeeded();
            }
            catch (Exception exception)
            {
                foreach (var alarmId in scheduledAlarmIds)
                {
                    TryCancel(alarmId);
                }

                return CalendarCommandResult.Failed(exception.Message);
            }
        }
    }

    public CalendarCommandResult DeleteEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return CalendarCommandResult.Failed("An event ID is required.");
        }

        lock (_gate)
        {
            if (_events.ResolveByIds([eventId]).Events.Count == 0)
            {
                return CalendarCommandResult.Failed($"Event '{eventId}' was not found.");
            }

            var eventSnapshot = _events.Snapshot();
            var reminderSnapshot = _reminders.Snapshot();
            var linkedReminders = reminderSnapshot
                .Where(reminder => string.Equals(reminder.CalendarEventId, eventId, StringComparison.Ordinal))
                .ToArray();
            var desiredEvents = eventSnapshot.Where(item => item.Id != eventId).ToArray();
            var desiredReminders = reminderSnapshot
                .Where(reminder => !string.Equals(reminder.CalendarEventId, eventId, StringComparison.Ordinal))
                .ToArray();

            try
            {
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    desiredEvents,
                    desiredReminders));
            }
            catch (Exception exception)
            {
                return CalendarCommandResult.Failed(exception.Message);
            }

            foreach (var reminder in linkedReminders)
            {
                if (reminder.AlarmId is int alarmId)
                {
                    TryCancel(alarmId);
                }
            }

            _events.ReplaceAll(desiredEvents);
            _reminders.ReplaceAll(desiredReminders);
            return CalendarCommandResult.Succeeded();
        }
    }

    public CalendarCommandResult CreateReminder(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        lock (_gate)
        {
            if (reminder.CalendarEventId is not null)
            {
                return CalendarCommandResult.Failed("Event-linked reminders must be managed through their calendar event.");
            }

            if (_reminders.Find(reminder.Id) is not null)
            {
                return CalendarCommandResult.Failed($"Reminder '{reminder.Id}' already exists.");
            }

            int? alarmId = null;
            try
            {
                alarmId = reminder.IsCompleted ? null : _alarms.Schedule(reminder);
                var persistedReminder = reminder with { AlarmId = alarmId };
                var desiredReminders = _reminders.Snapshot().Append(persistedReminder).ToArray();
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    _events.Snapshot(),
                    desiredReminders));
                _reminders.ReplaceAll(desiredReminders);
                return CalendarCommandResult.Succeeded();
            }
            catch (Exception exception)
            {
                if (alarmId is int scheduledAlarmId)
                {
                    TryCancel(scheduledAlarmId);
                }

                return CalendarCommandResult.Failed(exception.Message);
            }
        }
    }

    public CalendarCommandResult UpdateReminder(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        lock (_gate)
        {
            var existing = _reminders.Find(reminder.Id);
            if (existing is null)
            {
                return CalendarCommandResult.Failed($"Reminder '{reminder.Id}' was not found.");
            }

            if (existing.CalendarEventId is not null || reminder.CalendarEventId is not null)
            {
                return CalendarCommandResult.Failed("Event-linked reminders must be managed through their calendar event.");
            }

            int? newAlarmId = null;
            try
            {
                newAlarmId = reminder.IsCompleted ? null : _alarms.Schedule(reminder with { AlarmId = null });
                var persistedReminder = reminder with { AlarmId = newAlarmId };
                var desiredReminders = _reminders.Snapshot()
                    .Select(item => item.Id == reminder.Id ? persistedReminder : item)
                    .ToArray();
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    _events.Snapshot(),
                    desiredReminders));

                if (existing.AlarmId is int oldAlarmId)
                {
                    TryCancel(oldAlarmId);
                }

                _reminders.ReplaceAll(desiredReminders);
                return CalendarCommandResult.Succeeded();
            }
            catch (Exception exception)
            {
                if (newAlarmId is int scheduledAlarmId)
                {
                    TryCancel(scheduledAlarmId);
                }

                return CalendarCommandResult.Failed(exception.Message);
            }
        }
    }

    public CalendarCommandResult SetReminderCompleted(string reminderId, bool isCompleted)
    {
        if (string.IsNullOrWhiteSpace(reminderId))
        {
            return CalendarCommandResult.Failed("A reminder ID is required.");
        }

        var reminder = _reminders.Find(reminderId);
        return reminder is null
            ? CalendarCommandResult.Failed($"Reminder '{reminderId}' was not found.")
            : UpdateReminder(reminder with { IsCompleted = isCompleted });
    }

    public CalendarCommandResult DeleteReminder(string reminderId)
    {
        if (string.IsNullOrWhiteSpace(reminderId))
        {
            return CalendarCommandResult.Failed("A reminder ID is required.");
        }

        lock (_gate)
        {
            var existing = _reminders.Find(reminderId);
            if (existing is null)
            {
                return CalendarCommandResult.Failed($"Reminder '{reminderId}' was not found.");
            }

            if (existing.CalendarEventId is not null)
            {
                return CalendarCommandResult.Failed("Event-linked reminders must be managed through their calendar event.");
            }

            var desiredReminders = _reminders.Snapshot().Where(item => item.Id != reminderId).ToArray();
            try
            {
                _persistence.Save(new CalendarStoreDocument(
                    CalendarStoreDocument.CurrentSchemaVersion,
                    _events.Snapshot(),
                    desiredReminders));
            }
            catch (Exception exception)
            {
                return CalendarCommandResult.Failed(exception.Message);
            }

            if (existing.AlarmId is int alarmId)
            {
                TryCancel(alarmId);
            }

            _reminders.ReplaceAll(desiredReminders);
            return CalendarCommandResult.Succeeded();
        }
    }

    private static IReadOnlyList<int> NormalizeOffsets(IEnumerable<int> reminderOffsets)
    {
        var offsets = reminderOffsets.Distinct().ToArray();
        if (offsets.Any(offset => !CalendarReminder.AllowedOffsetMinutes.Contains(offset)))
        {
            throw new ArgumentException("Reminder offsets must be 10, 30, 60, or 1440 minutes.", nameof(reminderOffsets));
        }

        return offsets;
    }

    private static string LinkedReminderId(string eventId, int offsetMinutes) =>
        $"reminder:{eventId}:{offsetMinutes}";

    private void TryCancel(int alarmId)
    {
        try
        {
            _alarms.Cancel(alarmId);
        }
        catch
        {
            // Best-effort compensation; persistence and repositories were not published.
        }
    }
}
