namespace KeyloopScheduler.Domain.Abstractions.Models;

/// <summary>
/// A concrete pairing of a free bay and a free, certified technician for a
/// specific interval. Both resources are guaranteed unbooked for the window.
/// </summary>
public sealed record AvailabilitySlot(
    Guid ServiceBayId,
    Guid TechnicianId,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);
