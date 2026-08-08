using Reminder.Domain;
using Reminder.Persistence;

namespace Reminder.UseCases;

public sealed class ScheduleService
{
    private readonly object _gate = new();
    private readonly IScheduleStore _store;
    private readonly IScheduleResourceManager _resources;
    private readonly Func<DateTimeOffset> _clock;
    private ScheduleDocument _snapshot;

    public ScheduleService(IScheduleStore store, IScheduleResourceManager resources, Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _clock = clock ?? (() => DateTimeOffset.Now);
        _snapshot = _store.Load();
        _snapshot = ReconcileResources(_snapshot);
    }

    public event Action? Changed;

    public ScheduleDocument Snapshot
    {
        get { lock (_gate) return Copy(_snapshot); }
    }

    public CommandResult CreateReminder(ReminderItem reminder)
    {
        lock (_gate)
        {
            if (!ValidateReminder(reminder, requireFuture: true, out var invalid)) return invalid;
            var existing = _snapshot.Reminders.FirstOrDefault(x => x.Id == reminder.Id);
            if (existing is not null) return SameReminderPayload(existing, reminder)
                ? CommandResult.Ok("idempotent: reminder already exists")
                : CommandResult.Fail(ResultCode.Conflict, "conflict: reminder ID already has different content");

            string? newHandle = null;
            try
            {
                if (reminder.DueAt is not null) newHandle = _resources.CreateReminder(reminder);
                var desired = reminder with { ResourceHandle = newHandle };
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion, [.. _snapshot.Reminders, desired], _snapshot.Reservations));
                return CommandResult.Ok();
            }
            catch (Exception exception)
            {
                if (newHandle is not null) TryCancel(newHandle);
                return Internal(exception);
            }
        }
    }

    public CommandResult UpdateReminder(ReminderItem reminder)
    {
        lock (_gate)
        {
            if (!ValidateReminder(reminder, requireFuture: !reminder.Completed, out var invalid)) return invalid;
            var old = _snapshot.Reminders.FirstOrDefault(x => x.Id == reminder.Id);
            if (old is null) return CommandResult.Fail(ResultCode.NotFound, "not_found: reminder does not exist");
            if (old.Completed && !reminder.Completed) return CommandResult.Fail(ResultCode.Conflict, "conflict: completed reminders cannot be reopened by UpdateReminder");

            string? replacement = null;
            try
            {
                if (!reminder.Completed && reminder.DueAt is not null) replacement = _resources.CreateReminder(reminder);
                var desired = reminder with
                {
                    CreatedAt = old.CreatedAt,
                    CompletedAt = reminder.Completed ? old.CompletedAt ?? _clock() : null,
                    ResourceHandle = replacement,
                };
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion,
                    _snapshot.Reminders.Select(x => x.Id == reminder.Id ? desired : x).ToArray(), _snapshot.Reservations));
                if (old.ResourceHandle is not null) TryCancel(old.ResourceHandle);
                return CommandResult.Ok();
            }
            catch (Exception exception)
            {
                if (replacement is not null) TryCancel(replacement);
                return Internal(exception);
            }
        }
    }

    public CommandResult CompleteReminder(string id)
    {
        lock (_gate)
        {
            if (!ValidId(id)) return Invalid("invalid: a stable reminder ID is required");
            var old = _snapshot.Reminders.FirstOrDefault(x => x.Id == id);
            if (old is null) return CommandResult.Fail(ResultCode.NotFound, "not_found: reminder does not exist");
            if (old.Completed) return CommandResult.Ok("idempotent: reminder already completed");
            var desired = old with { Completed = true, CompletedAt = _clock(), ResourceHandle = null };
            try
            {
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion,
                    _snapshot.Reminders.Select(x => x.Id == id ? desired : x).ToArray(), _snapshot.Reservations));
                if (old.ResourceHandle is not null) TryCancel(old.ResourceHandle);
                return CommandResult.Ok();
            }
            catch (Exception exception) { return Internal(exception); }
        }
    }

    public CommandResult DeleteReminder(string id)
    {
        lock (_gate)
        {
            if (!ValidId(id)) return Invalid("invalid: a stable reminder ID is required");
            var old = _snapshot.Reminders.FirstOrDefault(x => x.Id == id);
            if (old is null) return CommandResult.Ok("idempotent: reminder already absent");
            try
            {
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion,
                    _snapshot.Reminders.Where(x => x.Id != id).ToArray(), _snapshot.Reservations));
                if (old.ResourceHandle is not null) TryCancel(old.ResourceHandle);
                return CommandResult.Ok();
            }
            catch (Exception exception) { return Internal(exception); }
        }
    }

    public IReadOnlyList<ReminderItem> SearchReminders(ReminderQuery query)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));
        if (query.Keyword?.Length > 200) throw new ArgumentException("Keyword cannot exceed 200 characters.", nameof(query));
        if (query.Limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query), "Limit must be 1 to 100.");
        var keyword = query.Keyword?.Trim() ?? string.Empty;
        var now = _clock();
        lock (_gate)
        {
            return _snapshot.Reminders
                .Where(x => keyword.Length == 0 || x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Note.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Where(x => MatchesCategory(x, query.Category, now))
                .OrderBy(x => x.Completed)
                .ThenBy(x => x.Completed
                    ? -(x.CompletedAt ?? x.CreatedAt).UtcTicks
                    : (x.DueAt ?? DateTimeOffset.MaxValue).UtcTicks)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .Take(query.Limit)
                .ToArray();
        }
    }

    public CommandResult AddReservation(ReservationItem reservation, ReservationKind expectedKind)
    {
        lock (_gate)
        {
            if (!ValidateReservation(reservation, expectedKind, out var invalid)) return invalid;
            var existing = _snapshot.Reservations.FirstOrDefault(x => x.Id == reservation.Id);
            if (existing is not null) return SameReservationPayload(existing, reservation)
                ? CommandResult.Ok("idempotent: reservation already exists")
                : CommandResult.Fail(ResultCode.Conflict, "conflict: reservation ID already has different content");
            string? handle = null;
            try
            {
                handle = _resources.CreateReservation(reservation);
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion, _snapshot.Reminders,
                    [.. _snapshot.Reservations, reservation with { ResourceHandle = handle }]));
                return CommandResult.Ok("Common-simulated: deterministic reservation created");
            }
            catch (Exception exception)
            {
                if (handle is not null) TryCancel(handle);
                return Internal(exception);
            }
        }
    }

    public CommandResult CancelReservation(string id, ReservationKind expectedKind)
    {
        lock (_gate)
        {
            if (!ValidId(id)) return Invalid("invalid: a stable reservation ID is required");
            var old = _snapshot.Reservations.FirstOrDefault(x => x.Id == id);
            if (old is null) return CommandResult.Ok("idempotent: reservation already absent");
            if (old.Kind != expectedKind) return CommandResult.Fail(ResultCode.Conflict, "conflict: reservation kind does not match Action");
            try
            {
                Publish(new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion, _snapshot.Reminders,
                    _snapshot.Reservations.Where(x => x.Id != id).ToArray()));
                if (old.ResourceHandle is not null) TryCancel(old.ResourceHandle);
                return CommandResult.Ok();
            }
            catch (Exception exception) { return Internal(exception); }
        }
    }

    public IReadOnlyList<ReservationItem> GetReservations()
    {
        var now = _clock();
        lock (_gate) return _snapshot.Reservations.Where(x => x.EndAt > now)
            .OrderBy(x => x.StartAt).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }

    private void Publish(ScheduleDocument desired)
    {
        _store.Save(desired);
        _snapshot = Copy(desired);
        Changed?.Invoke();
    }

    private ScheduleDocument ReconcileResources(ScheduleDocument loaded)
    {
        var now = _clock();
        var created = new List<string>();
        try
        {
            var reminders = loaded.Reminders.Select(item =>
            {
                string? replacement = null;
                if (!item.Completed && item.DueAt > now)
                {
                    replacement = _resources.CreateReminder(item);
                    if (replacement != item.ResourceHandle) created.Add(replacement);
                }
                return item with { ResourceHandle = replacement };
            }).ToArray();
            var reservations = loaded.Reservations.Select(item =>
            {
                string? replacement = null;
                if (item.EndAt > now)
                {
                    replacement = _resources.CreateReservation(item);
                    if (replacement != item.ResourceHandle) created.Add(replacement);
                }
                return item with { ResourceHandle = replacement };
            }).ToArray();
            var desired = new ScheduleDocument(ScheduleDocument.CurrentSchemaVersion, reminders, reservations);
            if (!SameHandles(loaded, desired)) _store.Save(desired);
            foreach (var old in loaded.Reminders.Select(x => x.ResourceHandle)
                         .Concat(loaded.Reservations.Select(x => x.ResourceHandle))
                         .Where(x => x is not null))
            {
                if (!reminders.Any(x => x.ResourceHandle == old) && !reservations.Any(x => x.ResourceHandle == old))
                    TryCancel(old!);
            }
            return desired;
        }
        catch
        {
            foreach (var handle in created) TryCancel(handle);
            throw;
        }
    }

    private static bool SameHandles(ScheduleDocument left, ScheduleDocument right) =>
        left.Reminders.Select(x => (x.Id, x.ResourceHandle)).SequenceEqual(right.Reminders.Select(x => (x.Id, x.ResourceHandle))) &&
        left.Reservations.Select(x => (x.Id, x.ResourceHandle)).SequenceEqual(right.Reservations.Select(x => (x.Id, x.ResourceHandle)));

    private static ScheduleDocument Copy(ScheduleDocument source) => new(source.SchemaVersion, source.Reminders.ToArray(), source.Reservations.ToArray());
    private static bool SameReminderPayload(ReminderItem a, ReminderItem b) => a.Id == b.Id && a.Title == b.Title.Trim() && a.DueAt == b.DueAt && a.Note == b.Note.Trim() && a.Completed == b.Completed;
    private static bool SameReservationPayload(ReservationItem a, ReservationItem b) => a.Id == b.Id && a.Kind == b.Kind && a.Channel == b.Channel.Trim() && a.Program == b.Program.Trim() && a.StartAt == b.StartAt && a.EndAt == b.EndAt && a.Repeat == b.Repeat;
    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;

    private bool ValidateReminder(ReminderItem? item, bool requireFuture, out CommandResult result)
    {
        if (item is null || !ValidId(item.Id) || string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 200 || item.Note.Length > 2000)
        { result = Invalid("invalid: reminder requires a stable ID and title within bounds"); return false; }
        if (requireFuture && item.DueAt is not null && item.DueAt <= _clock())
        { result = Invalid("invalid: due time must be in the future"); return false; }
        result = CommandResult.Ok(); return true;
    }

    private bool ValidateReservation(ReservationItem? item, ReservationKind expectedKind, out CommandResult result)
    {
        if (item is null || !ValidId(item.Id) || item.Kind != expectedKind || item.EndAt <= item.StartAt || item.StartAt <= _clock() ||
            (string.IsNullOrWhiteSpace(item.Channel) && string.IsNullOrWhiteSpace(item.Program)))
        { result = Invalid("invalid: reservation identity, kind, program/channel, and future time range are required"); return false; }
        result = CommandResult.Ok(); return true;
    }

    private static bool MatchesCategory(ReminderItem item, ReminderCategory category, DateTimeOffset now) => category switch
    {
        ReminderCategory.Today => !item.Completed && item.DueAt?.ToLocalTime().Date == now.ToLocalTime().Date,
        ReminderCategory.Upcoming => !item.Completed && item.DueAt >= now,
        ReminderCategory.Overdue => !item.Completed && item.DueAt < now,
        ReminderCategory.Completed => item.Completed,
        ReminderCategory.NoAlert => !item.Completed && item.DueAt is null,
        ReminderCategory.All => true,
        _ => false,
    };

    private static CommandResult Invalid(string reason) => CommandResult.Fail(ResultCode.Invalid, reason);
    private static CommandResult Internal(Exception exception) => CommandResult.Fail(ResultCode.Internal, "internal: " + exception.GetType().Name);
    private void TryCancel(string handle) { try { _resources.Cancel(handle); } catch { } }
}
