using System.ComponentModel.DataAnnotations;

namespace KeyloopScheduler.Api.Contracts;

/// <summary>
/// Request body for creating an appointment. Omitting <see cref="ServiceBayId"/>
/// or <see cref="TechnicianId"/> asks the engine to auto-assign the first
/// compatible free resource.
/// </summary>
public sealed class CreateAppointmentRequest
{
    /// <summary>Identifier of the owning dealership.</summary>
    [Required]
    public Guid DealershipId { get; init; }

    /// <summary>Identifier of the customer the appointment is booked for.</summary>
    [Required]
    public Guid CustomerId { get; init; }

    /// <summary>Identifier of the requested service type (determines duration and required certification).</summary>
    [Required]
    public Guid ServiceTypeId { get; init; }

    /// <summary>Optional specific service bay to reserve.</summary>
    public Guid? ServiceBayId { get; init; }

    /// <summary>Optional specific technician to reserve.</summary>
    public Guid? TechnicianId { get; init; }

    /// <summary>Standard 17-character Vehicle Identification Number.</summary>
    [Required]
    [StringLength(17, MinimumLength = 17)]
    public string Vin { get; init; } = string.Empty;

    /// <summary>Start of the appointment in strict UTC, quantized to 15 minutes, within 08:00-18:00 UTC.</summary>
    [Required]
    public DateTime StartTimeUtc { get; init; }
}
