using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// A bookable service definition: how long it takes and which certification a
/// technician must hold to perform it.
/// </summary>
public sealed class ServiceType
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public int DurationMinutes { get; private set; }

    public CertificationType RequiredCertification { get; private set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    private ServiceType()
    {
        // Required by EF Core materialization.
    }

    public ServiceType(Guid id, string name, int durationMinutes, CertificationType requiredCertification)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Service type id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Service type name must not be empty.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainValidationException("Service duration must be greater than zero.");
        }

        if (durationMinutes % BusinessHours.QuantizationMinutes != 0)
        {
            throw new DomainValidationException(
                $"Service duration must be a multiple of {BusinessHours.QuantizationMinutes} minutes.");
        }

        Id = id;
        Name = name.Trim();
        DurationMinutes = durationMinutes;
        RequiredCertification = requiredCertification;
    }
}
