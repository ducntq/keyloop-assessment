using KeyloopScheduler.Domain.Abstractions.Models;
using KeyloopScheduler.Domain.Common;

namespace KeyloopScheduler.Domain.Abstractions;

/// <summary>
/// Read-side contract for resource availability. Kept separate from the booking
/// command surface so availability search can later be served by a read replica
/// or a dedicated CQRS read model.
/// </summary>
public interface IResourceAvailabilityQuery
{
    /// <summary>True when no scheduled appointment occupies the bay for the window.</summary>
    Task<bool> IsServiceBayAvailableAsync(
        Guid serviceBayId,
        TimeWindow window,
        CancellationToken cancellationToken = default);

    /// <summary>True when no scheduled appointment occupies the technician for the window.</summary>
    Task<bool> IsTechnicianAvailableAsync(
        Guid technicianId,
        TimeWindow window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces all slots on the requested day where BOTH a bay and a certified
    /// technician are free for the full service duration.
    /// </summary>
    Task<IReadOnlyList<AvailabilitySlot>> FindAvailableSlotsAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken = default);
}
