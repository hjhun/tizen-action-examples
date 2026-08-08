using Calendar.Domain;

namespace Calendar.App;

public enum CalendarSurface
{
    Calendar,
    EventDetail,
    EventEditor,
    DeleteEventConfirmation,
    ReminderList,
    ReminderEditor,
    DeleteReminderConfirmation,
    Search,
}

public sealed record CalendarInteractionState(
    CalendarUiState Calendar,
    CalendarSurface Surface,
    string? SelectedEventId,
    CalendarEditorState? EventEditor,
    string? SelectedReminderId,
    CalendarReminderEditorState? ReminderEditor)
{
    public CalendarSearchState? Search { get; init; }

    public string? SearchReturnEventId { get; init; }

    public static CalendarInteractionState Create(CalendarUiState calendar) => new(
        calendar,
        CalendarSurface.Calendar,
        SelectedEventId: null,
        EventEditor: null,
        SelectedReminderId: null,
        ReminderEditor: null);

    public CalendarInteractionState OpenNewEvent() => this with
    {
        Surface = CalendarSurface.EventEditor,
        SelectedEventId = null,
        EventEditor = CalendarEditorState.CreateNew(Calendar.SelectedDate),
    };

    public CalendarInteractionState OpenEventDetail(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("An event ID is required.", nameof(eventId));
        }

        return this with
        {
            Surface = CalendarSurface.EventDetail,
            SelectedEventId = eventId,
            EventEditor = null,
        };
    }

    public CalendarInteractionState OpenEventEditor(CalendarEvent calendarEvent, IEnumerable<int> reminderOffsets)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(reminderOffsets);
        if (Surface != CalendarSurface.EventDetail || SelectedEventId != calendarEvent.Id)
        {
            throw new InvalidOperationException("Editing requires the selected event detail to be open.");
        }

        return this with
        {
            Surface = CalendarSurface.EventEditor,
            EventEditor = CalendarEditorState.CreateExisting(calendarEvent, reminderOffsets),
        };
    }

    public CalendarInteractionState RequestEventDelete()
    {
        if (Surface != CalendarSurface.EventDetail || SelectedEventId is null)
        {
            throw new InvalidOperationException("Event deletion requires an open event detail.");
        }

        return this with { Surface = CalendarSurface.DeleteEventConfirmation };
    }

    public CalendarInteractionState CancelEventDelete()
    {
        if (Surface != CalendarSurface.DeleteEventConfirmation || SelectedEventId is null)
        {
            throw new InvalidOperationException("No event deletion confirmation is open.");
        }

        return this with { Surface = CalendarSurface.EventDetail };
    }

    public CalendarInteractionState OpenReminderList()
    {
        if (Surface != CalendarSurface.Calendar)
        {
            throw new InvalidOperationException("Reminders can only be opened from the calendar.");
        }

        return this with
        {
            Surface = CalendarSurface.ReminderList,
            SelectedEventId = null,
            EventEditor = null,
            SelectedReminderId = null,
            ReminderEditor = null,
        };
    }

    public CalendarInteractionState OpenNewReminder(DateTimeOffset suggestedDue)
    {
        if (Surface != CalendarSurface.ReminderList)
        {
            throw new InvalidOperationException("A new reminder requires the reminder list to be open.");
        }

        return this with
        {
            Surface = CalendarSurface.ReminderEditor,
            SelectedReminderId = null,
            ReminderEditor = CalendarReminderEditorState.CreateNew(suggestedDue),
        };
    }

    public CalendarInteractionState OpenReminderEditor(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (Surface != CalendarSurface.ReminderList || reminder.CalendarEventId is not null)
        {
            throw new InvalidOperationException("Only an independent reminder from the reminder list can be edited.");
        }

        return this with
        {
            Surface = CalendarSurface.ReminderEditor,
            SelectedReminderId = reminder.Id,
            ReminderEditor = CalendarReminderEditorState.CreateExisting(reminder),
        };
    }

    public CalendarInteractionState RequestReminderDelete()
    {
        if (Surface != CalendarSurface.ReminderEditor || SelectedReminderId is null)
        {
            throw new InvalidOperationException("Reminder deletion requires an existing reminder editor.");
        }

        return this with { Surface = CalendarSurface.DeleteReminderConfirmation };
    }

    public CalendarInteractionState OpenSearch()
    {
        if (Surface != CalendarSurface.Calendar)
        {
            throw new InvalidOperationException("Search can only be opened from the calendar.");
        }

        return this with
        {
            Calendar = Calendar.FocusHeader(CalendarFocusRegion.Search),
            Surface = CalendarSurface.Search,
            Search = CalendarSearchState.Create(Calendar.VisibleMonth),
            SearchReturnEventId = null,
            SelectedEventId = null,
        };
    }

    public CalendarInteractionState OpenSearchResult(string eventId)
    {
        if (Surface != CalendarSurface.Search || Search is null ||
            !Search.ResultEventIds.Contains(eventId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("A result from the active search is required.");
        }

        return this with
        {
            Surface = CalendarSurface.EventDetail,
            SelectedEventId = eventId,
            EventEditor = null,
            SearchReturnEventId = eventId,
        };
    }

    public CalendarInteractionState Back() => Surface switch
    {
        CalendarSurface.Search => this with
        {
            Surface = CalendarSurface.Calendar,
            Search = null,
            SearchReturnEventId = null,
            Calendar = Calendar.FocusHeader(CalendarFocusRegion.Search),
        },
        CalendarSurface.DeleteEventConfirmation => CancelEventDelete(),
        CalendarSurface.EventEditor when SelectedEventId is not null => this with
        {
            Surface = CalendarSurface.EventDetail,
            EventEditor = null,
        },
        CalendarSurface.EventDetail when Search is not null => this with
        {
            Surface = CalendarSurface.Search,
            SelectedEventId = null,
            EventEditor = null,
        },
        CalendarSurface.EventEditor or CalendarSurface.EventDetail => this with
        {
            Surface = CalendarSurface.Calendar,
            SelectedEventId = null,
            EventEditor = null,
        },
        CalendarSurface.DeleteReminderConfirmation => this with
        {
            Surface = CalendarSurface.ReminderEditor,
        },
        CalendarSurface.ReminderEditor => this with
        {
            Surface = CalendarSurface.ReminderList,
            SelectedReminderId = null,
            ReminderEditor = null,
        },
        CalendarSurface.ReminderList => this with
        {
            Surface = CalendarSurface.Calendar,
            SelectedReminderId = null,
            ReminderEditor = null,
        },
        _ => this,
    };
}
