using KeyloopScheduler.Domain.ValueObjects;

namespace KeyloopScheduler.Domain.Abstractions.Models;

/// <summary>
/// Command describing a booking attempt. <see cref="ServiceBayId"/> and
/// <see cref="TechnicianId"/> are optional: when omitted the engine assigns the
/// first compatible free resource.
/// </summary>
public sealed record BookingRequest(
    Guid DealershipId,
    Guid ServiceTypeId,
    Guid? ServiceBayId,
    Guid? TechnicianId,
    Vin VehicleIdentification,
    DateTime StartTimeUtc);
