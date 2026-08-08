using System.Globalization;

namespace Calendar.Domain;

public static class CalendarSearchQueryAdapter
{
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    public static bool TryCreate(
        string? keyword,
        string? startDate,
        string? endDate,
        int requestedLimit,
        bool searchTitle,
        bool searchLocation,
        bool searchNote,
        out CalendarSearchCriteria? criteria,
        out string error)
    {
        criteria = null;
        if (!TryParseOptionalDate(startDate, out var startInclusive) ||
            !TryParseOptionalDate(endDate, out var endExclusive))
        {
            error = "StartDate and EndDate must be empty or valid ISO 8601 timestamps with an explicit UTC offset.";
            return false;
        }

        var hasExplicitFieldSelection = searchTitle || searchLocation || searchNote;
        try
        {
            criteria = CalendarSearchCriteria.Create(
                keyword,
                startInclusive,
                endExclusive,
                requestedLimit <= 0 ? 20 : Math.Min(requestedLimit, 100),
                hasExplicitFieldSelection ? searchTitle : true,
                hasExplicitFieldSelection ? searchLocation : true,
                hasExplicitFieldSelection ? searchNote : true);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryParseOptionalDate(string? value, out DateTimeOffset? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        var normalized = value.EndsWith("Z", StringComparison.Ordinal)
            ? $"{value[..^1]}+00:00"
            : value;
        if (DateTimeOffset.TryParseExact(
                normalized,
                TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            parsed = timestamp;
            return true;
        }

        parsed = null;
        return false;
    }
}
