using Calendar.Domain;

namespace Calendar.App;

public sealed record CalendarViewAnnotation(
    string EntityType,
    string EntityId,
    string EntityInfo);

public static class CalendarViewAnnotationRegistry
{
    public const string CalendarEntityType = "Tizen.Entity.Calendar";

    public static IReadOnlyList<CalendarViewAnnotation> Create(
        IEnumerable<CalendarEvent> visibleEvents,
        Func<CalendarEvent, string> toGeneratedEntityInfo)
    {
        ArgumentNullException.ThrowIfNull(visibleEvents);
        ArgumentNullException.ThrowIfNull(toGeneratedEntityInfo);

        return visibleEvents
            .Select(calendarEvent => new CalendarViewAnnotation(
                CalendarEntityType,
                calendarEvent.Id,
                toGeneratedEntityInfo(calendarEvent)))
            .ToArray();
    }
}
