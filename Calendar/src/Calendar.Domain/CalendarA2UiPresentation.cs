using System.Text.Json;

namespace Calendar.Domain;

public sealed record CalendarA2UiPresentation(string Template, string Document)
{
    public bool FitsTransport => Template.Length <= 65536 && Document.Length <= 65536;
}

public static class CalendarA2UiPresentations
{
    public static CalendarA2UiPresentation CreateReminder(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        var fields = new[] { "title", "due", "note", "state" };
        var components = new List<object>
        {
            new { id = "calendar-reminder", component = new { Column = new { children = new { explicitList = fields } } } },
        };
        components.AddRange(fields.Select(field => (object)new
        {
            id = field, component = new { Text = new { text = new { path = $"/{field}" } } },
        }));
        return new(
            JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "calendar-reminder", components } }),
            JsonSerializer.Serialize(new { dataModelUpdate = new
            {
                surfaceId = "calendar-reminder", path = "/",
                value = new { id = reminder.Id, title = reminder.Title, due = $"{reminder.DueAt:O}", note = reminder.Note, state = reminder.State },
            } }));
    }

    public static CalendarA2UiPresentation Create(IReadOnlyList<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count > 100) throw new ArgumentException("At most 100 events are allowed.", nameof(events));
        if (events.Count == 1) return Create(events[0]);

        var children = new List<string>();
        var components = new List<object>();
        var values = new Dictionary<string, object>();
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            var id = $"event-{index}";
            children.Add(id);
            var fields = new[] { "title", "time", "location", "note" };
            components.Add(new { id, component = new { Column = new { children = new { explicitList = fields.Select(field => $"{id}-{field}").ToArray() } } } });
            foreach (var field in fields)
                components.Add(new { id = $"{id}-{field}", component = new { Text = new { text = new { path = $"/{id}/{field}" } } } });
            values[id] = new { id = item.Id, title = item.Title, time = $"{item.Start:O} — {item.End:O}", location = item.Location, note = item.Note };
        }
        components.Insert(0, new { id = "calendar-events", component = new { Column = new { children = new { explicitList = children } } } });
        return new(
            JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "calendar-events", components } }),
            JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId = "calendar-events", path = "/", value = values } }));
    }

    public static CalendarA2UiPresentation Create(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var template = JsonSerializer.Serialize(new
        {
            surfaceUpdate = new
            {
                surfaceId = "calendar-event-card",
                components = new object[]
                {
                    new
                    {
                        id = "calendar-event-card",
                        component = new
                        {
                            Column = new
                            {
                                children = new { explicitList = new[] { "title", "time", "location", "note" } },
                            },
                        },
                    },
                    new { id = "title", component = new { Text = new { text = new { path = "/title" } } } },
                    new { id = "time", component = new { Text = new { text = new { path = "/time" } } } },
                    new { id = "location", component = new { Text = new { text = new { path = "/location" } } } },
                    new { id = "note", component = new { Text = new { text = new { path = "/note" } } } },
                },
            },
        });

        var document = JsonSerializer.Serialize(new
        {
            dataModelUpdate = new
            {
                surfaceId = "calendar-event-card",
                path = "/",
                value = new
                {
                    id = calendarEvent.Id,
                    title = calendarEvent.Title,
                    time = $"{calendarEvent.Start:O} — {calendarEvent.End:O}",
                    location = calendarEvent.Location,
                    note = calendarEvent.Note,
                },
            },
        });

        return new CalendarA2UiPresentation(template, document);
    }

    public static bool TryCreateFromGeneratedEntityJson(string entityJson, out CalendarA2UiPresentation presentation, string? expectedId = null)
    {
        presentation = default!;
        if (string.IsNullOrWhiteSpace(entityJson) || entityJson.Length > 65536)
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(entityJson);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                (!json.RootElement.TryGetProperty("TizenEntityCalendarEvent", out var entity) &&
                 !json.RootElement.TryGetProperty("TizenEntityCalendar", out entity)) ||
                entity.ValueKind != JsonValueKind.Object ||
                !TryGetString(entity, "Id", out var id) ||
                id.Length > 256 || (expectedId is not null && id != expectedId) ||
                !TryGetString(entity, "Title", out var title) ||
                !TryGetString(entity, "StartDate", out var startText) ||
                !TryGetString(entity, "EndDate", out var endText) ||
                !DateTimeOffset.TryParse(startText, out var start) ||
                !DateTimeOffset.TryParse(endText, out var end) ||
                end <= start || title.Length > 512)
            {
                return false;
            }

            TryGetString(entity, "Note", out var note);
            TryGetString(entity, "Location", out var location);
            if (note.Length > 4096 || location.Length > 512) return false;
            presentation = Create(CalendarEvent.Create(id, title, start, end, note, location));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement entity, string property, out string value)
    {
        value = string.Empty;
        return entity.TryGetProperty(property, out var jsonValue) &&
            jsonValue.ValueKind == JsonValueKind.String &&
            (value = jsonValue.GetString() ?? string.Empty) is not null;
    }
}
