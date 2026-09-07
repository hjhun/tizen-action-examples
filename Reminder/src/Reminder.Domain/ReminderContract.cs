using System.Globalization;

namespace Reminder.Domain;

// Portable validation shared by generated-provider adapters and host tests.
public static class ReminderContract
{
    public static ReminderQuery Query(string? id, string? keyword, string? category, int limit)
    {
        if (id?.Length > 128 || keyword?.Length > 200 || category?.Length > 128)
            throw new ArgumentException("Query ID/category must be at most 128 characters and keyword at most 200.");
        if (!string.IsNullOrWhiteSpace(category) && category is not
            ("Reminder" or "Tizen.Action.Reminder" or "org.tizen.reminder"))
            throw new ArgumentException("Category must identify Reminder or this app; it is not a smart-list filter.");
        return new(keyword ?? string.Empty, ReminderCategory.All, limit <= 0 ? 50 : Math.Min(limit, 100))
        { Id = id ?? string.Empty };
    }

    public static ReminderItem Item(string id, string title, string? dueDate, string? note, string? state)
    {
        DateTimeOffset? due = null;
        if (!string.IsNullOrWhiteSpace(dueDate))
        {
            if (dueDate.Length > 64 ||
                !(dueDate.EndsWith('Z') || (dueDate.Length >= 6 && dueDate[^6] is '+' or '-' && dueDate[^3] == ':')) ||
                !DateTimeOffset.TryParse(dueDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                throw new ArgumentException("DueDate must be RFC 3339 with an offset, or empty for no alert.");
            due = parsed;
        }
        return ReminderItem.Create(id, title, due, note).WithState(state);
    }
}
