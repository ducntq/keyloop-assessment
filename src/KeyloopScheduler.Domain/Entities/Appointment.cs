using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;
using KeyloopScheduler.Domain.ValueObjects;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// A reservation that binds one customer, one service bay, one qualified
/// technician and one vehicle to a contiguous interval of time.
/// </summary>
/// <remarks>
/// The interval is modelled by <see cref="TimeWindow"/> (which owns all interval
/// math and UTC validation) and persisted as two strict-UTC columns so that the
/// mandated compound indexes and SQL overlap predicates stay index-friendly.
/// </remarks>
public sealed class Appointment
{
    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public Guid ServiceBayId { get; private set; }

    public Guid TechnicianId { get; private set; }

    public Guid ServiceTypeId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Vin VehicleIdentification { get; private set; }

    public DateTime StartTimeUtc { get; private set; }

    public DateTime EndTimeUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Domain view over the persisted UTC interval (not mapped).</summary>
    public TimeWindow Window => new(StartTimeUtc, EndTimeUtc);

    /// <summary>Only scheduled appointments occupy resources.</summary>
    public bool OccupiesResources => Status == AppointmentStatus.Scheduled;

    private Appointment()
    {
        // Required by EF Core materialization.
    }

    private Appointment(
        Guid id,
        Guid dealershipId,
        Guid serviceBayId,
        Guid technicianId,
        Guid serviceTypeId,
        Guid customerId,
        Vin vehicleIdentification,
        TimeWindow window,
        DateTime createdAtUtc)
    {
        Id = id;
        DealershipId = dealershipId;
        ServiceBayId = serviceBayId;
        TechnicianId = technicianId;
        ServiceTypeId = serviceTypeId;
        CustomerId = customerId;
        VehicleIdentification = vehicleIdentification;
        StartTimeUtc = window.StartUtc;
        EndTimeUtc = window.EndUtc;
        Status = AppointmentStatus.Scheduled;
        CreatedAtUtc = createdAtUtc;
    }

    public static Appointment Schedule(
        Guid id,
        Guid dealershipId,
        Guid serviceBayId,
        Guid technicianId,
        Guid serviceTypeId,
        Guid customerId,
        Vin vehicleIdentification,
        TimeWindow window,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Appointment id must not be empty.");
        }

        if (dealershipId == Guid.Empty)
        {
            throw new DomainValidationException("Dealership id must not be empty.");
        }

        if (serviceBayId == Guid.Empty)
        {
            throw new DomainValidationException("Service bay id must not be empty.");
        }

        if (technicianId == Guid.Empty)
        {
            throw new DomainValidationException("Technician id must not be empty.");
        }

        if (serviceTypeId == Guid.Empty)
        {
            throw new DomainValidationException("Service type id must not be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainValidationException("Customer id must not be empty.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException("Created-at timestamp must be UTC.");
        }

        GuardBookableWindow(window);

        if (window.StartUtc < createdAtUtc)
        {
            throw new DomainValidationException("Appointment start must not be in the past.");
        }

        return new Appointment(
            id,
            dealershipId,
            serviceBayId,
            technicianId,
            serviceTypeId,
            customerId,
            vehicleIdentification,
            window,
            createdAtUtc);
    }

    /// <summary>
    /// Moves a scheduled appointment to a new interval, preserving all resource
    /// assignments. Cancelled and completed appointments cannot be rescheduled.
    /// </summary>
    public void Reschedule(TimeWindow newWindow)
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            throw new DomainValidationException(
                $"Only scheduled appointments can be rescheduled. Current status: {Status}.");
        }

        GuardBookableWindow(newWindow);

        StartTimeUtc = newWindow.StartUtc;
        EndTimeUtc = newWindow.EndUtc;
    }

    /// <summary>Cancels the appointment and releases its resources.</summary>
    public void Cancel(DateTime cancelledAtUtc)
    {
        if (cancelledAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException("Cancellation timestamp must be UTC.");
        }

        if (Status == AppointmentStatus.Cancelled)
        {
            throw new DomainValidationException("Appointment is already cancelled.");
        }

        if (Status == AppointmentStatus.Completed)
        {
            throw new DomainValidationException("A completed appointment cannot be cancelled.");
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    public void Complete()
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            throw new DomainValidationException(
                $"Only scheduled appointments can be completed. Current status: {Status}.");
        }

        Status = AppointmentStatus.Completed;
    }

    private static void GuardBookableWindow(TimeWindow window)
    {
        if (!BusinessHours.IsWithinOperatingHours(window))
        {
            throw new DomainValidationException(
                $"Appointment must fit within operating hours " +
                $"{BusinessHours.OpenUtc:hh\\:mm}-{BusinessHours.CloseUtc:hh\\:mm} UTC on a single day.");
        }

        if (!BusinessHours.IsQuantized(window.StartUtc))
        {
            throw new DomainValidationException(
                $"Appointment start must be quantized to {BusinessHours.QuantizationMinutes}-minute boundaries.");
        }
    }
}
