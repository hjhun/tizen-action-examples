namespace Calendar.Domain;

/// <summary>
/// Thread-safe store for independent and event-linked reminders. UI callbacks and Action-provider
/// calls arrive on different threads, so every read and mutation is taken under one lock and every
/// returned collection is an immutable copy.
/// </summary>
public sealed class CalendarReminderRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CalendarReminder> _remindersById;

    public CalendarReminderRepository(IEnumerable<CalendarReminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        var remindersById = new Dictionary<string, CalendarReminder>(StringComparer.Ordinal);
        foreach (var reminder in reminders)
        {
            if (!remindersById.TryAdd(reminder.Id, reminder))
            {
                throw new ArgumentException($"Duplicate reminder ID: {reminder.Id}", nameof(reminders));
            }
        }

        _remindersById = remindersById;
    }

    public CalendarReminder? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_gate)
        {
            return _remindersById.TryGetValue(id, out var reminder) ? reminder : null;
        }
    }

    public bool TryAdd(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        lock (_gate)
        {
            return _remindersById.TryAdd(reminder.Id, reminder);
        }
    }

    public bool TryUpdate(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        lock (_gate)
        {
            if (!_remindersById.ContainsKey(reminder.Id))
            {
                return false;
            }

            _remindersById[reminder.Id] = reminder;
            return true;
        }
    }

    public IReadOnlyList<CalendarReminder> FindByCalendarEventId(string? calendarEventId)
    {
        if (string.IsNullOrWhiteSpace(calendarEventId))
        {
            return [];
        }

        lock (_gate)
        {
            return OrderedLocked()
                .Where(reminder => string.Equals(reminder.CalendarEventId, calendarEventId, StringComparison.Ordinal))
                .ToArray();
        }
    }

    public IReadOnlyList<CalendarReminder> Search(string? term)
    {
        var trimmedTerm = term?.Trim() ?? string.Empty;

        lock (_gate)
        {
            return OrderedLocked()
                .Where(reminder => trimmedTerm.Length == 0 || Matches(reminder, trimmedTerm))
                .ToArray();
        }
    }

    /// <summary>Returns an immutable ordered copy for persistence, provider responses, and rollback.</summary>
    public IReadOnlyList<CalendarReminder> Snapshot()
    {
        lock (_gate)
        {
            return OrderedLocked().ToArray();
        }
    }

    /// <summary>Restores the repository to a previously captured snapshot, used for transaction rollback.</summary>
    public void ReplaceAll(IEnumerable<CalendarReminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        var replacement = new Dictionary<string, CalendarReminder>(StringComparer.Ordinal);
        foreach (var reminder in reminders)
        {
            if (!replacement.TryAdd(reminder.Id, reminder))
            {
                throw new ArgumentException($"Duplicate reminder ID: {reminder.Id}", nameof(reminders));
            }
        }

        lock (_gate)
        {
            _remindersById.Clear();
            foreach (var pair in replacement)
            {
                _remindersById.Add(pair.Key, pair.Value);
            }
        }
    }

    private static bool Matches(CalendarReminder reminder, string term) =>
        reminder.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        reminder.Note.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>Marks a reminder complete and drops its alarm metadata, because its alarm is cancelled.</summary>
    public bool TryComplete(string id) => TrySetCompleted(id, isCompleted: true);

    public bool TryReopen(string id) => TrySetCompleted(id, isCompleted: false);

    private bool TrySetCompleted(string id, bool isCompleted)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_gate)
        {
            if (!_remindersById.TryGetValue(id, out var reminder))
            {
                return false;
            }

            _remindersById[id] = reminder with { IsCompleted = isCompleted, AlarmId = null };
            return true;
        }
    }

    private IOrderedEnumerable<CalendarReminder> OrderedLocked() =>
        _remindersById.Values
            .OrderBy(reminder => reminder.IsCompleted)
            .ThenBy(reminder => reminder.DueAt)
            .ThenBy(reminder => reminder.Id, StringComparer.Ordinal);

    public bool TryDelete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_gate)
        {
            return _remindersById.Remove(id);
        }
    }
}
