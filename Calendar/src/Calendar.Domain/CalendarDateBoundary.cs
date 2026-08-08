namespace Calendar.Domain;

public static class CalendarDateBoundary
{
    public static DateTimeOffset AtStartOfDay(DateOnly date, TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? TimeZoneInfo.Local;
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        // Some historical zones move clocks at midnight. A local date then begins at
        // the first valid wall-clock instant rather than at an invalid 00:00.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
