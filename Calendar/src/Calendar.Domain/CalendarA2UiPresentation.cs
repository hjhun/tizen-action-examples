using System.Text.Json;

namespace Calendar.Domain;

public sealed record CalendarA2UiPresentation(string Template, string Document);

public static class CalendarA2UiPresentations
{
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

    public static bool TryCreateFromGeneratedEntityJson(string entityJson, out CalendarA2UiPresentation presentation)
    {
        presentation = default!;
        if (string.IsNullOrWhiteSpace(entityJson))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(entityJson);
            if (!json.RootElement.TryGetProperty("TizenEntityCalendar", out var entity) ||
                !TryGetString(entity, "Id", out var id) ||
                !TryGetString(entity, "Title", out var title) ||
                !TryGetString(entity, "StartDate", out var startText) ||
                !TryGetString(entity, "EndDate", out var endText) ||
                !DateTimeOffset.TryParse(startText, out var start) ||
                !DateTimeOffset.TryParse(endText, out var end) ||
                end <= start)
            {
                return false;
            }

            TryGetString(entity, "Note", out var note);
            TryGetString(entity, "Location", out var location);
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
