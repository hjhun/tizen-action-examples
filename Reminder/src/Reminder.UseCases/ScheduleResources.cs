using Reminder.Domain;

namespace Reminder.UseCases;

public interface IScheduleResourceManager
{
    string CreateReminder(ReminderItem reminder);
    string CreateReservation(ReservationItem reservation);
    void Cancel(string handle);
}

public sealed class DeterministicReservationSimulator : IScheduleResourceManager
{
    public List<string> CreatedHandles { get; } = [];
    public List<string> CancelledHandles { get; } = [];

    public string CreateReminder(ReminderItem reminder)
    {
        var handle = $"reminder:{reminder.Id}";
        CreatedHandles.Add(handle);
        return handle;
    }

    public string CreateReservation(ReservationItem reservation)
    {
        var handle = $"reservation:{reservation.Id}:{reservation.Kind.ToString().ToLowerInvariant()}";
        CreatedHandles.Add(handle);
        return handle;
    }

    public void Cancel(string handle) => CancelledHandles.Add(handle);
}
