using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarViewAnnotation(
    string EntityType,
    string EntityId,
    string EntityJson);

public static class CalendarViewAnnotationRegistry
{
    public const string CalendarEntityType = "Tizen.Entity.Calendar";

    public static IReadOnlyList<CalendarViewAnnotation> Create(
        IEnumerable<CalendarEvent> visibleEvents,
        Func<CalendarEvent, string> toGeneratedEntityJson)
    {
        ArgumentNullException.ThrowIfNull(visibleEvents);
        ArgumentNullException.ThrowIfNull(toGeneratedEntityJson);

        return visibleEvents
            .Select(calendarEvent => new CalendarViewAnnotation(
                CalendarEntityType,
                calendarEvent.Id,
                toGeneratedEntityJson(calendarEvent)))
            .ToArray();
    }
}
