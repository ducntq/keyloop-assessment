namespace KeyloopScheduler.Domain.Abstractions.Models;

/// <summary>
/// Outcome of a successful booking, carrying the concrete resources that were
/// allocated so the client can display and later act on them.
/// </summary>
public sealed record BookingResult(
    Guid AppointmentId,
    Guid DealershipId,
    Guid CustomerId,
    Guid ServiceBayId,
    Guid TechnicianId,
    Guid ServiceTypeId,
    string VehicleIdentification,
    string Status,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);
