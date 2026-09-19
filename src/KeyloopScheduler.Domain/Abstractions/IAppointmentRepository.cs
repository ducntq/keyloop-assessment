using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Entities;

namespace KeyloopScheduler.Domain.Abstractions;

/// <summary>
/// Command-side persistence contract for appointments. Implementations are
/// responsible for SQL-level overlap detection; callers must never filter
/// appointments in memory.
/// </summary>
public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);

    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a <see cref="Entities.Appointment"/> in <c>Scheduled</c> state
    /// overlaps the window for the given bay or technician. Evaluated in the database.
    /// </summary>
    Task<bool> HasResourceConflictAsync(
        Guid serviceBayId,
        Guid technicianId,
        TimeWindow window,
        Guid? excludeAppointmentId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountScheduledAsync(CancellationToken cancellationToken = default);
}
