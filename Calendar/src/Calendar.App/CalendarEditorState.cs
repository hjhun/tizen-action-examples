using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarEditorState(
    string? EventId,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Location,
    string Note,
    IReadOnlySet<int> ReminderOffsets)
{
    private static readonly IReadOnlySet<int> AllowedReminderOffsets = new HashSet<int> { 10, 30, 60, 1440 };

    public bool IsEditing => EventId is not null;

    public bool CanSave => ValidationMessage is null;

    public string? ValidationMessage => string.IsNullOrWhiteSpace(Title)
        ? "Title is required."
        : End <= Start
            ? "End time must be after start time."
            : null;

    public static CalendarEditorState CreateNew(DateOnly date)
    {
        var start = CalendarDateBoundary.AtStartOfDay(date).AddHours(9);
        return new CalendarEditorState(
            EventId: null,
            Title: string.Empty,
            Start: start,
            End: start.AddHours(1),
            Location: string.Empty,
            Note: string.Empty,
            ReminderOffsets: new HashSet<int>());
    }

    public static CalendarEditorState CreateExisting(
        CalendarEvent calendarEvent,
        IEnumerable<int> reminderOffsets)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(reminderOffsets);

        var offsets = reminderOffsets.ToHashSet();
        if (offsets.Any(offset => !AllowedReminderOffsets.Contains(offset)))
        {
            throw new ArgumentOutOfRangeException(nameof(reminderOffsets));
        }

        return new CalendarEditorState(
            calendarEvent.Id,
            calendarEvent.Title,
            calendarEvent.Start,
            calendarEvent.End,
            calendarEvent.Location,
            calendarEvent.Note,
            offsets);
    }

    public CalendarEditorState WithTitle(string title) => this with { Title = title ?? string.Empty };

    public CalendarEditorState WithRange(DateTimeOffset start, DateTimeOffset end) => this with
    {
        Start = start,
        End = end,
    };

    public CalendarEditorState ToggleReminder(int offsetMinutes)
    {
        if (!AllowedReminderOffsets.Contains(offsetMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(offsetMinutes));
        }

        var offsets = new HashSet<int>(ReminderOffsets);
        if (!offsets.Add(offsetMinutes))
        {
            offsets.Remove(offsetMinutes);
        }

        return this with { ReminderOffsets = offsets };
    }
}
