using KeyloopScheduler.Domain.Abstractions.Models;

namespace KeyloopScheduler.Domain.Abstractions;

/// <summary>
/// Write-side application service that atomically reserves a bay and a qualified
/// technician, or fails with a conflict. This is the only entry point through
/// which appointments may be created or cancelled.
/// </summary>
public interface IAppointmentBookingService
{
    /// <summary>
    /// Reserves both resources for the requested window. Throws
    /// <see cref="Exceptions.ScheduleConflictException"/> when either resource is
    /// unavailable (including under concurrent contention).
    /// </summary>
    Task<BookingResult> BookAsync(BookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels an appointment, releasing its resources.</summary>
    Task CancelAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}
