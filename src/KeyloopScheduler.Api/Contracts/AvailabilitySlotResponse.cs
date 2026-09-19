using KeyloopScheduler.Domain.Abstractions.Models;

namespace KeyloopScheduler.Api.Contracts;

/// <summary>
/// A bookable pairing of a free service bay and a free, certified technician.
/// </summary>
public sealed record AvailabilitySlotResponse(
    Guid ServiceBayId,
    Guid TechnicianId,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc)
{
    public static AvailabilitySlotResponse From(AvailabilitySlot slot) =>
        new(slot.ServiceBayId, slot.TechnicianId, slot.StartTimeUtc, slot.EndTimeUtc);
}
