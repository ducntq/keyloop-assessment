using System.Diagnostics;
using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Abstractions.Models;
using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Exceptions;
using KeyloopScheduler.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace KeyloopScheduler.Infrastructure.Services;

/// <summary>
/// Application service that atomically reserves a service bay and a qualified
/// technician for a requested window. Resource selection happens optimistically
/// outside the transaction and is re-validated inside a serializable transaction
/// immediately before the insert, which is what makes concurrent double-booking
/// impossible.
/// </summary>
internal sealed class AppointmentBookingService : IAppointmentBookingService
{
    private readonly IAppointmentRepository _appointments;
    private readonly IResourceCatalogQuery _catalog;
    private readonly IResourceAvailabilityQuery _availability;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITechnicianQualificationRule _qualificationRule;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AppointmentBookingService> _logger;

    public AppointmentBookingService(
        IAppointmentRepository appointments,
        IResourceCatalogQuery catalog,
        IResourceAvailabilityQuery availability,
        IUnitOfWork unitOfWork,
        ITechnicianQualificationRule qualificationRule,
        TimeProvider timeProvider,
        ILogger<AppointmentBookingService> logger)
    {
        _appointments = appointments;
        _catalog = catalog;
        _availability = availability;
        _unitOfWork = unitOfWork;
        _qualificationRule = qualificationRule;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<BookingResult> BookAsync(
        BookingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await BookCoreAsync(request, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Booking outcome {Outcome} DealershipId={DealershipId} CustomerId={CustomerId} " +
                "ServiceTypeId={ServiceTypeId} ServiceBayId={ServiceBayId} TechnicianId={TechnicianId} " +
                "AppointmentId={AppointmentId} DurationMs={DurationMs}",
                "Booked",
                result.DealershipId,
                result.CustomerId,
                result.ServiceTypeId,
                result.ServiceBayId,
                result.TechnicianId,
                result.AppointmentId,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (ScheduleConflictException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "Booking outcome {Outcome} DealershipId={DealershipId} ServiceTypeId={ServiceTypeId} DurationMs={DurationMs}",
                "Conflict",
                request.DealershipId,
                request.ServiceTypeId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private async Task<BookingResult> BookCoreAsync(
        BookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartTimeUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException("Start time must be expressed in UTC.");
        }

        if (!await _catalog.DealershipExistsAsync(request.DealershipId, cancellationToken))
        {
            throw EntityNotFoundException.For("Dealership", request.DealershipId);
        }

        var serviceType = await _catalog.GetServiceTypeAsync(request.ServiceTypeId, cancellationToken)
            ?? throw EntityNotFoundException.For("ServiceType", request.ServiceTypeId);

        var customer = await _catalog.GetCustomerAsync(request.CustomerId, cancellationToken)
            ?? throw EntityNotFoundException.For("Customer", request.CustomerId);

        if (customer.DealershipId != request.DealershipId)
        {
            throw new DomainValidationException(
                "The requested customer does not belong to the requested dealership.");
        }

        if (!customer.IsActive)
        {
            throw new DomainValidationException($"Customer '{customer.FullName}' is not active.");
        }

        // Constructing the window validates UTC kind, ordering and duration.
        var window = new TimeWindow(request.StartTimeUtc, request.StartTimeUtc.Add(serviceType.Duration));

        var bay = await ResolveServiceBayAsync(request, window, cancellationToken);
        var technician = await ResolveTechnicianAsync(request, serviceType, window, cancellationToken);

        var appointmentId = Guid.NewGuid();
        var createdAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Second, in-transaction check: the serializable transaction serializes
                // competing writers, so exactly one of them can observe "no conflict".
                var conflict = await _appointments.HasResourceConflictAsync(
                    bay.Id,
                    technician.Id,
                    window,
                    excludeAppointmentId: null,
                    token);

                if (conflict)
                {
                    throw new ScheduleConflictException(
                        $"Service bay '{bay.Name}' or technician '{technician.Name}' is already booked " +
                        "for the requested window.");
                }

                var appointment = Appointment.Schedule(
                    appointmentId,
                    request.DealershipId,
                    bay.Id,
                    technician.Id,
                    serviceType.Id,
                    customer.Id,
                    request.VehicleIdentification,
                    window,
                    createdAtUtc);

                await _appointments.AddAsync(appointment, token);

                return new BookingResult(
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
            },
            cancellationToken);
    }

    public async Task CancelAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new DomainValidationException("Appointment id must not be empty.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var appointment = await _appointments.GetByIdAsync(appointmentId, token)
                    ?? throw EntityNotFoundException.For("Appointment", appointmentId);

                appointment.Cancel(_timeProvider.GetUtcNow().UtcDateTime);
                return true;
            },
            cancellationToken);
    }

    private async Task<ServiceBay> ResolveServiceBayAsync(
        BookingRequest request,
        TimeWindow window,
        CancellationToken cancellationToken)
    {
        if (request.ServiceBayId is { } bayId)
        {
            var bay = await _catalog.GetServiceBayAsync(bayId, cancellationToken)
                ?? throw EntityNotFoundException.For("ServiceBay", bayId);

            if (bay.DealershipId != request.DealershipId)
            {
                throw new DomainValidationException(
                    "The requested service bay does not belong to the requested dealership.");
            }

            if (!bay.IsActive)
            {
                throw new DomainValidationException($"Service bay '{bay.Name}' is not active.");
            }

            return bay;
        }

        var candidates = await _catalog.GetActiveServiceBaysAsync(request.DealershipId, cancellationToken);
        foreach (var candidate in candidates)
        {
            if (await _availability.IsServiceBayAvailableAsync(candidate.Id, window, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ScheduleConflictException("No service bay is available for the requested window.");
    }

    private async Task<Technician> ResolveTechnicianAsync(
        BookingRequest request,
        ServiceType serviceType,
        TimeWindow window,
        CancellationToken cancellationToken)
    {
        if (request.TechnicianId is { } technicianId)
        {
            var technician = await _catalog.GetTechnicianAsync(technicianId, cancellationToken)
                ?? throw EntityNotFoundException.For("Technician", technicianId);

            if (technician.DealershipId != request.DealershipId)
            {
                throw new DomainValidationException(
                    "The requested technician does not belong to the requested dealership.");
            }

            if (!technician.IsActive)
            {
                throw new DomainValidationException($"Technician '{technician.Name}' is not active.");
            }

            if (!_qualificationRule.IsQualified(technician, serviceType))
            {
                throw new DomainValidationException(
                    $"Technician '{technician.Name}' does not hold the " +
                    $"'{serviceType.RequiredCertification}' certification required by '{serviceType.Name}'.");
            }

            return technician;
        }

        var candidates = await _catalog.GetActiveTechniciansAsync(request.DealershipId, cancellationToken);
        foreach (var candidate in candidates)
        {
            if (!_qualificationRule.IsQualified(candidate, serviceType))
            {
                continue;
            }

            if (await _availability.IsTechnicianAvailableAsync(candidate.Id, window, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ScheduleConflictException(
            $"No technician certified for '{serviceType.Name}' is available for the requested window.");
    }
}
