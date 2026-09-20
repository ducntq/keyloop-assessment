using KeyloopScheduler.Domain.Abstractions.Models;
using KeyloopScheduler.Domain.Entities;

namespace KeyloopScheduler.Api.Contracts;

/// <summary>
/// Representation of a scheduled appointment returned to clients.
/// </summary>
public sealed record AppointmentResponse(
    Guid AppointmentId,
    Guid DealershipId,
    Guid CustomerId,
    Guid ServiceBayId,
    Guid TechnicianId,
    Guid ServiceTypeId,
    string VehicleIdentification,
    string Status,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc)
{
    public static AppointmentResponse From(BookingResult result) =>
        new(
            result.AppointmentId,
            result.DealershipId,
            result.CustomerId,
            result.ServiceBayId,
            result.TechnicianId,
            result.ServiceTypeId,
            result.VehicleIdentification,
            result.Status,
            result.StartTimeUtc,
            result.EndTimeUtc);

    public static AppointmentResponse From(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.DealershipId,
            appointment.CustomerId,
            appointment.ServiceBayId,
            appointment.TechnicianId,
            appointment.ServiceTypeId,
            appointment.VehicleIdentification.Value,
            appointment.Status.ToString(),
            appointment.StartTimeUtc,
            appointment.EndTimeUtc);
}
