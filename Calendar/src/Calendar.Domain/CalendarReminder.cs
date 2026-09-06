namespace Calendar.Domain;

/// <summary>
/// An app-owned immutable reminder. Independent reminders and event-linked reminders share this shape;
/// only event-linked reminders carry <see cref="CalendarEventId"/> and <see cref="OffsetMinutes"/>.
/// </summary>
public sealed record CalendarReminder(
    string Id,
    string Title,
    DateTimeOffset DueAt,
    string Note,
    bool IsCompleted,
    string? CalendarEventId,
    int? OffsetMinutes,
    int? AlarmId)
{
    // Old persisted records omit this member and retain their bool completion flag.
    public string IncompleteState { get; init; } = "To-do";

    [System.Text.Json.Serialization.JsonIgnore]
    public string State => IsCompleted ? "Done" : IncompleteState;

    public CalendarReminder WithState(string? state) => state switch
    {
        "To-do" or "In-progress" or "Blocked" => this with { IsCompleted = false, IncompleteState = state },
        "Done" => this with { IsCompleted = true },
        _ => throw new ArgumentException("Reminder state must be To-do, In-progress, Blocked, or Done.", nameof(state)),
    };

    /// <summary>The reminder offsets an event editor may attach: 10 minutes, 30 minutes, 1 hour, and 1 day.</summary>
    public static IReadOnlyList<int> AllowedOffsetMinutes { get; } = [10, 30, 60, 1440];

    public static CalendarReminder CreateForEvent(
        string id,
        string title,
        DateTimeOffset eventStart,
        string calendarEventId,
        int offsetMinutes,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(calendarEventId))
        {
            throw new ArgumentException("A linked calendar event ID is required.", nameof(calendarEventId));
        }

        if (!AllowedOffsetMinutes.Contains(offsetMinutes))
        {
            throw new ArgumentException(
                $"An event-linked reminder offset must be one of {string.Join(", ", AllowedOffsetMinutes)} minutes.",
                nameof(offsetMinutes));
        }

        return Create(id, title, eventStart.AddMinutes(-offsetMinutes), note) with
        {
            CalendarEventId = calendarEventId,
            OffsetMinutes = offsetMinutes,
        };
    }

    public static CalendarReminder Create(
        string id,
        string title,
        DateTimeOffset dueAt,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A reminder ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A reminder title is required.", nameof(title));
        }

        return new CalendarReminder(
            id,
            title.Trim(),
            dueAt,
            note?.Trim() ?? string.Empty,
            IsCompleted: false,
            CalendarEventId: null,
            OffsetMinutes: null,
            AlarmId: null);
    }
}
