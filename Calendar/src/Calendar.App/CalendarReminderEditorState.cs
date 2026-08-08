using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarReminderEditorState(
    string? ReminderId,
    string Title,
    DateTimeOffset DueAt,
    string Note,
    bool IsCompleted)
{
    public bool IsEditing => ReminderId is not null;

    public string? ValidationMessage => string.IsNullOrWhiteSpace(Title)
        ? "Title is required."
        : DueAt == default
            ? "A due date is required."
            : null;

    public bool CanSave => ValidationMessage is null;

    public static CalendarReminderEditorState CreateNew(DateTimeOffset suggestedDue) => new(
        ReminderId: null,
        Title: string.Empty,
        DueAt: suggestedDue,
        Note: string.Empty,
        IsCompleted: false);

    public static CalendarReminderEditorState CreateExisting(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        return new CalendarReminderEditorState(
            reminder.Id,
            reminder.Title,
            reminder.DueAt,
            reminder.Note,
            reminder.IsCompleted);
    }

    public CalendarReminderEditorState WithTitle(string title) => this with { Title = title ?? string.Empty };

    public CalendarReminder ToDomain(string stableId) => CalendarReminder.Create(
        ReminderId ?? stableId,
        Title,
        DueAt,
        Note) with { IsCompleted = IsCompleted };
}
