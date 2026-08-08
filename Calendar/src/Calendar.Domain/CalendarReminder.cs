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
