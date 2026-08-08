namespace Calendar.Domain;

public sealed record CalendarEvent(
    string Id,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Note,
    string Location)
{
    public TimeSpan Duration => End - Start;

    public static CalendarEvent Create(
        string id,
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        string note,
        string location)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An event ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An event title is required.", nameof(title));
        }

        if (end <= start)
        {
            throw new ArgumentException("An event must end after it starts.", nameof(end));
        }

        return new CalendarEvent(id, title.Trim(), start, end, note?.Trim() ?? string.Empty, location?.Trim() ?? string.Empty);
    }
}
