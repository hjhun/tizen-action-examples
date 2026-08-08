#nullable enable

using Calendar.Domain;
using RPCPort.ViewActionProvider;
using RPCPort.ViewActionProvider.Stub;
using CalendarEntity = RPCPort.CalendarActionProvider.TizenEntityCalendar;

namespace Calendar.ViewActionProvider;

public sealed class CalendarViewService : TizenInternalActionView.ServiceBase
{
    public override void OnCreate()
    {
    }

    public override void OnTerminate()
    {
    }

    public override TizenEntityStatus FindById(string id, out TizenEntityView view)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            view = new TizenEntityView();
            return Failure("A view ID is required.");
        }

        return CalendarViewProviderState.TryFind(id, out view)
            ? Success()
            : Failure("The requested view is not currently visible.");
    }

    public override TizenEntityStatus GetAnnotatedViews(out List<TizenEntityView> views)
    {
        views = CalendarViewProviderState.GetAnnotatedViews();
        return Success();
    }

    public override TizenEntityStatus GetFocusedView(out TizenEntityView view)
    {
        return CalendarViewProviderState.TryGetFocused(out view)
            ? Success()
            : Failure("No annotated calendar view is currently focused.");
    }

    public override TizenEntityStatus ToPresentation(TizenEntityView view, out TizenEntityPresentation result)
    {
        if (view?.HasAnnotation != true ||
            view.Annotation is null ||
            view.Annotation.EntityType != CalendarViewProviderState.CalendarEntityType ||
            !CalendarA2UiPresentations.TryCreateFromGeneratedEntityJson(view.Annotation.EntityJson, out var presentation))
        {
            result = new TizenEntityPresentation();
            return Failure("A valid Calendar ViewAnnotation with generated EntityJson is required.");
        }

        result = new TizenEntityPresentation
        {
            Template = presentation.Template,
            Document = presentation.Document,
        };
        return Success();
    }

    private static TizenEntityStatus Success() => new() { Success = true, Reason = string.Empty };

    private static TizenEntityStatus Failure(string reason) => new() { Success = false, Reason = reason };
}

internal static class CalendarViewProviderState
{
    internal const string CalendarEntityType = "Tizen.Entity.Calendar";
    private static readonly object Gate = new();
    private static IReadOnlyList<TizenEntityView> _visibleViews = [];

    internal static void PublishVisibleEventViews(IEnumerable<CalendarEventViewSnapshot> visibleViews, string? focusedEventId)
    {
        ArgumentNullException.ThrowIfNull(visibleViews);

        var published = visibleViews
            .Where(snapshot =>
                double.IsFinite(snapshot.X) &&
                double.IsFinite(snapshot.Y) &&
                double.IsFinite(snapshot.Width) &&
                double.IsFinite(snapshot.Height) &&
                snapshot.Width > 0 &&
                snapshot.Height > 0)
            .GroupBy(snapshot => snapshot.Event.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(snapshot => ToAnnotatedView(snapshot, snapshot.Event.Id == focusedEventId))
            .ToArray();

        lock (Gate)
        {
            _visibleViews = published;
        }
    }

    internal static bool TryFind(string id, out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _visibleViews.FirstOrDefault(candidate => candidate.Id == id) ?? new TizenEntityView();
            return view.Id is not null;
        }
    }

    internal static List<TizenEntityView> GetAnnotatedViews()
    {
        lock (Gate)
        {
            return _visibleViews.ToList();
        }
    }

    internal static bool TryGetFocused(out TizenEntityView view)
    {
        lock (Gate)
        {
            view = _visibleViews.FirstOrDefault(candidate => candidate.IsFocused) ?? new TizenEntityView();
            return view.Id is not null;
        }
    }

    private static TizenEntityView ToAnnotatedView(CalendarEventViewSnapshot snapshot, bool isFocused)
    {
        var calendarEvent = snapshot.Event;
        var entity = new CalendarEntity
        {
            Id = calendarEvent.Id,
            Extra = string.Empty,
            Title = calendarEvent.Title,
            StartDate = calendarEvent.Start.ToString("O"),
            EndDate = calendarEvent.End.ToString("O"),
            Note = calendarEvent.Note,
            Location = calendarEvent.Location,
        };

        return new TizenEntityView
        {
            Id = $"calendar:event:{calendarEvent.Id}",
            Extra = string.Empty,
            Type = "Calendar.EventCard",
            Description = calendarEvent.Title,
            Bounds = new Bounds
            {
                X = snapshot.X,
                Y = snapshot.Y,
                Width = snapshot.Width,
                Height = snapshot.Height,
            },
            IsFocused = isFocused,
            IsEnabled = true,
            IsVisible = true,
            HasAnnotation = true,
            Annotation = new Annotation
            {
                EntityType = CalendarEntityType,
                EntityId = calendarEvent.Id,
                EntityJson = entity.ToJson(),
            },
        };
    }
}
