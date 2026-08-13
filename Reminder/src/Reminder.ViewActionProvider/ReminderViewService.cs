#nullable enable
using System.Text.Json;
using Reminder.Domain;
using RPCPort.ReminderViewActionProvider;
using RPCPort.ReminderViewActionProvider.Stub;
using ScheduleReminder = RPCPort.ReminderScheduleActionProvider.TizenEntityReminder;
using ScheduleReservation = RPCPort.ReminderScheduleActionProvider.TizenEntityReservation;
using ScheduleChannel = RPCPort.ReminderScheduleActionProvider.TizenEntityChannel;
using ScheduleProgram = RPCPort.ReminderScheduleActionProvider.TizenEntityProgram;

namespace Reminder.ViewActionProvider;

public sealed class ReminderViewService : TizenActionView.ServiceBase
{
    public override void OnCreate() { }
    public override void OnTerminate() { }

    public override TizenEntityStatus FindById(string id, out TizenEntityView v)
    {
        if (string.IsNullOrWhiteSpace(id)) { v = EmptyView(); return Failure("invalid: view ID is required"); }
        if (ReminderViewState.TryFind(id, out v)) return Success();
        v = EmptyView();
        return Failure("not_found: view is not currently visible");
    }

    public override TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = ReminderViewState.All();
        return Success();
    }

    public override TizenEntityStatus GetFocusedView(out TizenEntityView v)
    {
        if (ReminderViewState.TryFocused(out v)) return Success();
        v = EmptyView();
        return Failure("not_found: no annotated view is focused");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityView view, out TizenEntityPresentation result)
    {
        if (view?.Annotation is null || string.IsNullOrWhiteSpace(view.Annotation.EntityInfo))
        { result = EmptyPresentation(); return Failure("invalid: annotated View with EntityInfo is required"); }
        var surfaceId = view.Id ?? "reminder-surface";
        result = new TizenEntityPresentation
        {
            Template = JsonSerializer.Serialize(new
            {
                surfaceUpdate = new { surfaceId, components = new[] { new { id = "entity", component = "Text", text = view.Description ?? string.Empty } } },
            }),
            Document = JsonSerializer.Serialize(new
            {
                dataModelUpdate = new { surfaceId, entityType = view.Annotation.EntityType, entityId = view.Annotation.EntityId, entityInfo = view.Annotation.EntityInfo },
            }),
        };
        return Success();
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };
    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
    private static TizenEntityPresentation EmptyPresentation() => new() { Document = string.Empty, Template = string.Empty };
    private static TizenEntityView EmptyView() => new()
    {
        Id = string.Empty,
        Extra = string.Empty,
        Type = string.Empty,
        Description = string.Empty,
        ScreenBounds = new ScreenBounds(),
        WindowBounds = new WindowBounds(),
        IsFocused = false,
        IsEnabled = false,
        Annotation = new Annotation { EntityId = string.Empty, EntityType = string.Empty, EntityInfo = string.Empty },
    };
}

internal static class ReminderViewState
{
    private static readonly object Gate = new();
    private static IReadOnlyList<TizenEntityView> _views = [];

    internal static void Publish(IEnumerable<ReminderViewSnapshot> snapshots)
    {
        var views = snapshots.Where(IsValid)
            .GroupBy(x => x.ViewId, StringComparer.Ordinal).Select(x => x.First())
            .Select(ToView).ToArray();
        lock (Gate) _views = views;
    }

    internal static bool TryFind(string id, out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _views.FirstOrDefault(x => x.Id == id) ?? new();
            return view.Id is not null;
        }
    }

    internal static bool TryFocused(out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _views.FirstOrDefault(x => x.IsFocused) ?? new();
            return view.Id is not null;
        }
    }

    internal static List<TizenEntityView> All() { lock (Gate) return _views.ToList(); }

    private static bool IsValid(ReminderViewSnapshot x) => !string.IsNullOrWhiteSpace(x.ViewId) &&
        (x.Reminder is not null ^ x.Reservation is not null) &&
        double.IsFinite(x.ScreenX) && double.IsFinite(x.ScreenY) && double.IsFinite(x.Width) && double.IsFinite(x.Height) && x.Width > 0 && x.Height > 0;
    private static string EntityId(ReminderViewSnapshot x) => x.Reminder?.Id ?? x.Reservation!.Id;

    private static TizenEntityView ToView(ReminderViewSnapshot snapshot)
    {
        var reminder = snapshot.Reminder;
        var reservation = snapshot.Reservation;
        var entityType = reminder is not null ? "Tizen.Entity.Reminder" : "Tizen.Entity.Reservation";
        var entityId = EntityId(snapshot);
        var entityInfo = reminder is not null ? new ScheduleReminder
        {
            Id = reminder.Id, Extra = string.Empty, Title = reminder.Title,
            DueDate = reminder.DueAt?.ToString("O") ?? string.Empty,
            Note = snapshot.IncludeNote ? reminder.Note : string.Empty, Completed = reminder.Completed,
        }.ToJson() : new ScheduleReservation
        {
            Id = reservation!.Id, Extra = string.Empty,
            Channel = new ScheduleChannel { Id = reservation.Channel, Extra = string.Empty, Name = reservation.Channel },
            Program = new ScheduleProgram { Id = reservation.Program, Extra = string.Empty, Title = reservation.Program },
            StartTime = reservation.StartAt.ToString("O"), EndTime = reservation.EndAt.ToString("O"),
            Repeat = reservation.Repeat.ToString().ToLowerInvariant(), Kind = reservation.Kind.ToString().ToLowerInvariant(),
        }.ToJson();
        return new TizenEntityView
        {
            Id = snapshot.ViewId,
            Extra = string.Empty,
            Type = reminder is not null ? "Reminder.Card" : "Reminder.ReservationCard",
            Description = reminder?.Title ?? reservation!.Program,
            ScreenBounds = new ScreenBounds { X = snapshot.ScreenX, Y = snapshot.ScreenY, Width = snapshot.Width, Height = snapshot.Height },
            WindowBounds = snapshot.WindowX is { } x && snapshot.WindowY is { } y && double.IsFinite(x) && double.IsFinite(y)
                ? new WindowBounds { X = x, Y = y, Width = snapshot.Width, Height = snapshot.Height } : null,
            IsFocused = snapshot.IsFocused,
            IsEnabled = true,
            Annotation = new Annotation { EntityType = entityType, EntityId = entityId, EntityInfo = entityInfo },
        };
    }
}
