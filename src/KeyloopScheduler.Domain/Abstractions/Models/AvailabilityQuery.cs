namespace KeyloopScheduler.Domain.Abstractions.Models;

/// <summary>
/// Query for bookable slots on a single service day.
/// </summary>
public sealed record AvailabilityQuery(
    Guid DealershipId,
    Guid ServiceTypeId,
    DateOnly Date);
