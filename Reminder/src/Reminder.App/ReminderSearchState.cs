namespace Reminder.App;

internal sealed record ReminderSearchState(string DraftKeyword = "", string AppliedKeyword = "")
{
    internal ReminderSearchState WithDraft(string keyword) => this with { DraftKeyword = keyword };
    internal ReminderSearchState Apply() => this with { AppliedKeyword = DraftKeyword };
}
