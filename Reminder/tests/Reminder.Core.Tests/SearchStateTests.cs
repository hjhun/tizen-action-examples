using Reminder.App;
using Reminder.Domain;
using Reminder.Persistence;
using Reminder.UseCases;

internal static class SearchStateTests
{
    internal static void Run()
    {
        var service = new ScheduleService(new MemoryScheduleStore(), new DeterministicReservationSimulator());
        service.CreateReminder(ReminderItem.Create("milk", "Buy milk", null, ""));
        service.CreateReminder(ReminderItem.Create("call", "Call home", null, ""));
        var state = new ReminderSearchState();
        string[] Results() => service.SearchReminders(new ReminderQuery(state.AppliedKeyword, ReminderCategory.All, 50))
            .Select(x => x.Id).Order().ToArray();
        var shown = Results();
        state = state.WithDraft("milk");
        Check(state.DraftKeyword == "milk" && state.AppliedKeyword == "" && Results().SequenceEqual(shown),
            "Typing must expose the draft without changing the current list or item annotations.");
        state = state.Apply();
        Check(Results().SequenceEqual(["milk"]), "Apply must filter the visible list.");
        state = state.WithDraft("no match");
        Check(Results().SequenceEqual(["milk"]), "An unapplied empty result must retain the shown entity.");
        state = state.Apply();
        Check(Results().Length == 0, "Applying an unmatched keyword must produce the empty list.");
        state = state.WithDraft("");
        Check(Results().Length == 0, "Clearing the input is still a draft until Apply.");
        state = state.Apply();
        Check(Results().SequenceEqual(shown), "Applying a cleared keyword restores all items.");
        Console.WriteLine("SearchStateTests: PASS (draft, apply, empty result, clear)");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
