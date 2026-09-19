using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// A human resource that can service one appointment at a time when they hold
/// the certification required by the service type.
/// </summary>
public sealed class Technician
{
    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public List<CertificationType> Certifications { get; private set; } = new();

    private Technician()
    {
        // Required by EF Core materialization.
    }

    public Technician(Guid id, Guid dealershipId, string name, IEnumerable<CertificationType> certifications)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Technician id must not be empty.");
        }

        if (dealershipId == Guid.Empty)
        {
            throw new DomainValidationException("Dealership id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Technician name must not be empty.");
        }

        Id = id;
        DealershipId = dealershipId;
        Name = name.Trim();
        IsActive = true;
        Certifications = certifications?.Distinct().ToList() ?? new List<CertificationType>();
    }

    public bool HasCertification(CertificationType certification) =>
        Certifications.Contains(certification);

    public void GrantCertification(CertificationType certification)
    {
        if (!Certifications.Contains(certification))
        {
            Certifications.Add(certification);
        }
    }

    public void RevokeCertification(CertificationType certification) =>
        Certifications.Remove(certification);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
