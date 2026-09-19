using System.ComponentModel.DataAnnotations;

namespace KeyloopScheduler.Api.Contracts;

/// <summary>
/// Query string parameters for the availability search.
/// </summary>
public sealed class AvailabilityRequest
{
    /// <summary>Identifier of the owning dealership.</summary>
    [Required]
    public Guid DealershipId { get; init; }

    /// <summary>Identifier of the service type being searched for.</summary>
    [Required]
    public Guid ServiceTypeId { get; init; }

    /// <summary>Service day to search, in ISO format (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly Date { get; init; }
}
