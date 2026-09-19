using FluentAssertions;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: technician certification set behaviour.
/// </summary>
[Trait("Category", "Domain")]
public sealed class TechnicianTests
{
    private static readonly Guid Id = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DealershipId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public void Creates_an_active_technician_with_distinct_certifications()
    {
        var technician = new Technician(
            Id,
            DealershipId,
            "Tech B",
            new[] { CertificationType.General, CertificationType.Brakes, CertificationType.General });

        technician.IsActive.Should().BeTrue();
        technician.Certifications.Should().BeEquivalentTo(
            new[] { CertificationType.General, CertificationType.Brakes });
    }

    [Fact]
    public void HasCertification_reflects_the_certification_set()
    {
        var technician = new Technician(Id, DealershipId, "Tech D", new[] { CertificationType.Diesel, CertificationType.Brakes });

        technician.HasCertification(CertificationType.Diesel).Should().BeTrue();
        technician.HasCertification(CertificationType.EvCertified).Should().BeFalse();
    }

    [Fact]
    public void Grant_and_revoke_update_the_certification_set()
    {
        var technician = new Technician(Id, DealershipId, "Tech A", new[] { CertificationType.General });

        technician.GrantCertification(CertificationType.Brakes);
        technician.GrantCertification(CertificationType.Brakes);
        technician.Certifications.Should().BeEquivalentTo(
            new[] { CertificationType.General, CertificationType.Brakes });

        technician.RevokeCertification(CertificationType.General);
        technician.HasCertification(CertificationType.General).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_removes_the_technician_from_availability()
    {
        var technician = new Technician(Id, DealershipId, "Tech C", new[] { CertificationType.EvCertified });

        technician.Deactivate();

        technician.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Rejects_empty_identity()
    {
        var emptyId = () => new Technician(Guid.Empty, DealershipId, "Tech", Array.Empty<CertificationType>());
        var emptyName = () => new Technician(Id, DealershipId, " ", Array.Empty<CertificationType>());

        emptyId.Should().Throw<DomainValidationException>();
        emptyName.Should().Throw<DomainValidationException>();
    }
}
