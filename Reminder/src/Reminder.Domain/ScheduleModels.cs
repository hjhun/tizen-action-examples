namespace Reminder.Domain;

public enum ReminderCategory { Today, Upcoming, Overdue, Completed, NoAlert, All }
public enum ReservationKind { Viewing, Recording }
public enum ReservationRepeat { Once, Daily, Weekly, Weekdays }

public sealed record ReminderItem(
    string Id,
    string Title,
    DateTimeOffset? DueAt,
    string Note,
    bool Completed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ResourceHandle)
{
    public static ReminderItem Create(string id, string title, DateTimeOffset? dueAt, string? note, DateTimeOffset? createdAt = null)
    {
        ValidateId(id);
        var normalizedTitle = title?.Trim() ?? string.Empty;
        if (normalizedTitle.Length is < 1 or > 200) throw new ArgumentException("Title must contain 1 to 200 characters.", nameof(title));
        var normalizedNote = note?.Trim() ?? string.Empty;
        if (normalizedNote.Length > 2000) throw new ArgumentException("Note cannot exceed 2000 characters.", nameof(note));
        return new(id, normalizedTitle, dueAt, normalizedNote, false, createdAt ?? DateTimeOffset.UtcNow, null, null);
    }

    public static void ValidateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("ID must contain 1 to 128 characters.", nameof(id));
    }
}

public sealed record ReservationItem(
    string Id,
    ReservationKind Kind,
    string Channel,
    string Program,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    ReservationRepeat Repeat,
    string? ResourceHandle)
{
    public static ReservationItem Create(string id, ReservationKind kind, string? channel, string? program, DateTimeOffset startAt, DateTimeOffset endAt, ReservationRepeat repeat)
    {
        ReminderItem.ValidateId(id);
        var normalizedChannel = channel?.Trim() ?? string.Empty;
        var normalizedProgram = program?.Trim() ?? string.Empty;
        if (normalizedChannel.Length == 0 && normalizedProgram.Length == 0) throw new ArgumentException("Channel or program is required.");
        if (normalizedChannel.Length > 200 || normalizedProgram.Length > 200) throw new ArgumentException("Channel and program cannot exceed 200 characters.");
        if (endAt <= startAt) throw new ArgumentException("End time must follow start time.");
        return new(id, kind, normalizedChannel, normalizedProgram, startAt, endAt, repeat, null);
    }
}

public sealed record ReminderQuery(string Keyword, ReminderCategory Category, int Limit);

public sealed record ScheduleDocument(int SchemaVersion, IReadOnlyList<ReminderItem> Reminders, IReadOnlyList<ReservationItem> Reservations)
{
    public const int CurrentSchemaVersion = 1;
    public static ScheduleDocument Empty { get; } = new(CurrentSchemaVersion, [], []);
}
