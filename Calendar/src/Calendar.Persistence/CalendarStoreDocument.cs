using Calendar.Domain;

namespace Calendar.Persistence;

public sealed record CalendarStoreDocument(
    int SchemaVersion,
    IReadOnlyList<CalendarEvent> Events,
    IReadOnlyList<CalendarReminder> Reminders)
{
    public const int CurrentSchemaVersion = 1;
}
