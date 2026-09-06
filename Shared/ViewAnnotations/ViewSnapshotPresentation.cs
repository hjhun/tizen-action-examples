#nullable enable
using System.Text.Json;

namespace ActionExamples.ViewAnnotations;

// Preserve the apps' legacy v0.8 split Template/Document profile. This is not v0.9.1.
public static class ViewSnapshotPresentation
{
    public static bool TryCreate(string entityType, string entityId, string entityInfo, out string template, out string document)
    {
        template = document = string.Empty;
        if (string.IsNullOrWhiteSpace(entityId) || entityId.Length > 1024 || string.IsNullOrEmpty(entityInfo) || entityInfo.Length > 65536) return false;
        var wrapper = entityType switch
        {
            "Tizen.Entity" => "TizenEntity",
            "Tizen.Entity.Reminder" => "TizenEntityReminder",
            "Tizen.Entity.Reservation" => "TizenEntityReservation",
            _ => null,
        };
        if (wrapper is null) return false;
        try
        {
            using var json = JsonDocument.Parse(entityInfo);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty(wrapper, out var entity) ||
                entity.ValueKind != JsonValueKind.Object || !entity.TryGetProperty("Id", out var id) ||
                id.ValueKind != JsonValueKind.String || id.GetString() != entityId) return false;
            var values = new Dictionary<string, string>();
            if (entityType == "Tizen.Entity")
            {
                if (!entity.TryGetProperty("Extra", out var extra) || extra.ValueKind != JsonValueKind.String) return false;
                using var state = JsonDocument.Parse(extra.GetString() ?? string.Empty);
                if (state.RootElement.ValueKind != JsonValueKind.Object ||
                    !state.RootElement.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number != 1 ||
                    !state.RootElement.TryGetProperty("page", out var page) || page.ValueKind != JsonValueKind.Object) return false;
                AddFields(page, "page", values);
                if (state.RootElement.TryGetProperty("draft", out var draft)) AddFields(draft, "draft", values);
            }
            else
            {
                foreach (var field in new[] { "Title", "DueDate", "Note", "State", "Completed", "Program", "Channel", "StartTime", "EndTime", "Repeat", "Kind" })
                    if (entity.TryGetProperty(field, out var value)) AddFields(value, field, values);
            }
            if (values.Count == 0) return false;
            var surfaceId = "view-context";
            var components = new List<object>
            {
                new { id = "root", component = new { Column = new { children = new { explicitList = values.Keys.ToArray() } } } },
            };
            components.AddRange(values.Keys.Select(key => (object)new { id = key, component = new { Text = new { text = new { path = "/" + key } } } }));
            template = JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId, components } });
            document = JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId, path = "/", value = values } });
            if (template.Length > 65536 || document.Length > 65536)
            {
                template = document = string.Empty;
                return false;
            }
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static void AddFields(JsonElement value, string name, Dictionary<string, string> values, int depth = 0)
    {
        if (depth > 5 || values.Count >= 64) return;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in value.EnumerateObject())
                if (field.Name is not "Extra" and not "Id") AddFields(field.Value, name + "." + field.Name, values, depth + 1);
        }
        else if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString();
            values["field" + values.Count] = name + ": " + text[..Math.Min(4096, text.Length)];
        }
    }
}
