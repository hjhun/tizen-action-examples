using Calendar.Domain;
using Calendar.UseCases;
using Tizen.Applications;
using Tizen.Applications.Notifications;

namespace Calendar.App;

internal sealed class TizenReminderAlarmScheduler : IReminderAlarmScheduler
{
    public int? Schedule(CalendarReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (reminder.IsCompleted || reminder.DueAt <= DateTimeOffset.Now)
        {
            return null;
        }

        var notification = new Notification
        {
            Tag = $"calendar-reminder:{reminder.Id}",
            Title = reminder.Title,
            Content = string.IsNullOrWhiteSpace(reminder.Note) ? "Calendar reminder" : reminder.Note,
        };
        var alarm = AlarmManager.CreateAlarm(reminder.DueAt.LocalDateTime, notification);
        return alarm.AlarmId;
    }

    public void Cancel(int alarmId)
    {
        var alarm = AlarmManager.GetAllScheduledAlarms().SingleOrDefault(item => item.AlarmId == alarmId);
        alarm?.Cancel();
    }

}
