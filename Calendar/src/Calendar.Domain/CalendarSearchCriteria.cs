namespace Calendar.Domain;

public sealed record CalendarSearchCriteria(
    string Keyword,
    DateTimeOffset? StartInclusive,
    DateTimeOffset? EndExclusive,
    int Limit,
    bool SearchTitle,
    bool SearchLocation,
    bool SearchNote)
{
    public static CalendarSearchCriteria Create(
        string? keyword,
        DateTimeOffset? startInclusive,
        DateTimeOffset? endExclusive,
        int limit,
        bool searchTitle = true,
        bool searchLocation = true,
        bool searchNote = true)
    {
        var trimmedKeyword = keyword?.Trim() ?? string.Empty;
        if (trimmedKeyword.Length > 512)
        {
            throw new ArgumentException("The search keyword must not exceed 512 characters.", nameof(keyword));
        }

        if (startInclusive is not null && endExclusive is not null && endExclusive <= startInclusive)
        {
            throw new ArgumentException("The search period end must be after its start.", nameof(endExclusive));
        }

        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The search limit must be between 1 and 100.");
        }

        if (!searchTitle && !searchLocation && !searchNote)
        {
            throw new ArgumentException("At least one calendar text field must be selected.");
        }

        return new CalendarSearchCriteria(
            trimmedKeyword,
            startInclusive,
            endExclusive,
            limit,
            searchTitle,
            searchLocation,
            searchNote);
    }
}
