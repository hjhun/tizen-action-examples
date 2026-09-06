namespace Reminder.App;

internal sealed record ReminderAnnotationPage(string Id, string Title, object State)
{
    internal static ReminderAnnotationPage Create(string section, bool editing, bool newItem, string? selectedId, string keyword, string timeFilter, bool confirmDelete = false)
    {
        var mode = confirmDelete ? "delete-confirmation" : editing ? (newItem ? "new" : "edit") : selectedId is null ? "list" : "detail";
        return new($"reminder:page:{section}:{mode}", $"{section} · {mode}",
            new { section, mode, keyword, timeFilter, selectedId });
    }
}
