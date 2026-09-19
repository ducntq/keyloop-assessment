namespace KeyloopScheduler.Domain.Enums;

/// <summary>
/// Lifecycle state of an appointment. Only <see cref="Scheduled"/> appointments
/// occupy physical and human resources.
/// </summary>
public enum AppointmentStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2
}
