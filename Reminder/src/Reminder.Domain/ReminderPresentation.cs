using System.Text.Json;

namespace Reminder.Domain;

// Explicit legacy v0.8 split Presentation profile supported by DisplayPresentation.
public sealed record ReminderPresentation(string Template, string Document)
{
    public static ReminderPresentation Create(ReminderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var fields = new[] { "title", "due", "note", "state" };
        var components = new List<object>
        {
            new { id = "root", component = new { Column = new { children = new { explicitList = fields } } } },
        };
        components.AddRange(fields.Select(field => (object)new
        {
            id = field, component = new { Text = new { text = new { path = "/" + field } } },
        }));
        return new(
            JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId = "reminder", components } }),
            JsonSerializer.Serialize(new { dataModelUpdate = new
            {
                surfaceId = "reminder", path = "/",
                value = new { id = item.Id, title = item.Title, due = item.DueAt?.ToString("O") ?? "No alert", note = item.Note, state = item.State },
            } }));
    }
}
