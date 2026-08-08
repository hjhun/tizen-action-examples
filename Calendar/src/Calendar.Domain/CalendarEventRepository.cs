namespace Calendar.Domain;

public sealed record CalendarEventResolution(
    IReadOnlyList<CalendarEvent> Events,
    IReadOnlyList<string> UnresolvedIds);

public sealed record CalendarSearchSnapshot(
    IReadOnlyList<CalendarEvent> Events,
    long RepositoryVersion);

public sealed class CalendarEventRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CalendarEvent> _eventsById;
    private long _version;

    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public CalendarEventRepository(IEnumerable<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventsById = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);
        foreach (var calendarEvent in events)
        {
            if (!eventsById.TryAdd(calendarEvent.Id, calendarEvent))
            {
                throw new ArgumentException($"Duplicate calendar event ID: {calendarEvent.Id}", nameof(events));
            }
        }

        _eventsById = eventsById;
    }

    public bool TryAdd(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        lock (_gate)
        {
            if (!_eventsById.TryAdd(calendarEvent.Id, calendarEvent))
            {
                return false;
            }

            _version++;
            return true;
        }
    }

    public bool TryUpdate(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        lock (_gate)
        {
            if (!_eventsById.ContainsKey(calendarEvent.Id))
            {
                return false;
            }

            _eventsById[calendarEvent.Id] = calendarEvent;
            _version++;
            return true;
        }
    }

    public bool TryDelete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_gate)
        {
            if (!_eventsById.Remove(id))
            {
                return false;
            }

            _version++;
            return true;
        }
    }

    /// <summary>Returns an immutable ordered copy for persistence, provider responses, and rollback.</summary>
    public IReadOnlyList<CalendarEvent> Snapshot()
    {
        lock (_gate)
        {
            return _eventsById.Values
                .OrderBy(calendarEvent => calendarEvent.Start)
                .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>Restores the repository to a previously captured snapshot, used for transaction rollback.</summary>
    public void ReplaceAll(IEnumerable<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var replacement = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);
        foreach (var calendarEvent in events)
        {
            if (!replacement.TryAdd(calendarEvent.Id, calendarEvent))
            {
                throw new ArgumentException($"Duplicate calendar event ID: {calendarEvent.Id}", nameof(events));
            }
        }

        lock (_gate)
        {
            _eventsById.Clear();
            foreach (var pair in replacement)
            {
                _eventsById.Add(pair.Key, pair.Value);
            }
            _version++;
        }
    }

    public IReadOnlyList<CalendarEvent> Search(string? term)
    {
        var trimmedTerm = term?.Trim() ?? string.Empty;

        lock (_gate)
        {
            return _eventsById.Values
                .Where(calendarEvent => trimmedTerm.Length == 0 || MatchesAll(calendarEvent, trimmedTerm))
                .OrderBy(calendarEvent => calendarEvent.Start)
                .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public IReadOnlyList<CalendarEvent> Search(CalendarSearchCriteria criteria)
        => SearchWithVersion(criteria).Events;

    public CalendarSearchSnapshot SearchWithVersion(CalendarSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        lock (_gate)
        {
            var events = _eventsById.Values
                .Where(calendarEvent => criteria.Keyword.Length == 0 || Matches(calendarEvent, criteria))
                .Where(calendarEvent => criteria.StartInclusive is null || calendarEvent.End > criteria.StartInclusive)
                .Where(calendarEvent => criteria.EndExclusive is null || calendarEvent.Start < criteria.EndExclusive)
                .OrderBy(calendarEvent => calendarEvent.Start)
                .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
                .Take(criteria.Limit)
                .ToArray();
            return new CalendarSearchSnapshot(events, _version);
        }
    }

    private static bool Matches(CalendarEvent calendarEvent, CalendarSearchCriteria criteria) =>
        (criteria.SearchTitle && calendarEvent.Title.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase)) ||
        (criteria.SearchLocation && calendarEvent.Location.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase)) ||
        (criteria.SearchNote && calendarEvent.Note.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAll(CalendarEvent calendarEvent, string term) =>
        calendarEvent.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        calendarEvent.Location.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        calendarEvent.Note.Contains(term, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<CalendarEvent> GetEventsOverlapping(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive)
    {
        if (endExclusive <= startInclusive)
        {
            throw new ArgumentException("The period end must be after its start.", nameof(endExclusive));
        }

        lock (_gate)
        {
            return _eventsById.Values
                .Where(calendarEvent => calendarEvent.Start < endExclusive && calendarEvent.End > startInclusive)
                .OrderBy(calendarEvent => calendarEvent.Start)
                .ThenBy(calendarEvent => calendarEvent.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public CalendarEventResolution ResolveByIds(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var resolvedEvents = new List<CalendarEvent>();
        var unresolvedIds = new List<string>();

        lock (_gate)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !_eventsById.TryGetValue(id, out var calendarEvent))
                {
                    unresolvedIds.Add(id ?? string.Empty);
                    continue;
                }

                resolvedEvents.Add(calendarEvent);
            }
        }

        return new CalendarEventResolution(resolvedEvents, unresolvedIds);
    }
}
