using FluentAssertions;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: service catalogue invariants.
/// </summary>
[Trait("Category", "Domain")]
public sealed class ServiceTypeTests
{
    private static readonly Guid Id = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public void Creates_a_valid_service_type()
    {
        var serviceType = new ServiceType(Id, "Brake Pad Replacement", 60, CertificationType.Brakes);

        serviceType.Name.Should().Be("Brake Pad Replacement");
        serviceType.DurationMinutes.Should().Be(60);
        serviceType.Duration.Should().Be(TimeSpan.FromMinutes(60));
        serviceType.RequiredCertification.Should().Be(CertificationType.Brakes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Rejects_non_positive_duration(int duration)
    {
        var act = () => new ServiceType(Id, "Oil & Inspection", duration, CertificationType.General);

        act.Should().Throw<DomainValidationException>().WithMessage("*greater than zero*");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(70)]
    public void Rejects_duration_not_aligned_to_quantization(int duration)
    {
        var act = () => new ServiceType(Id, "Oil & Inspection", duration, CertificationType.General);

        act.Should().Throw<DomainValidationException>().WithMessage("*multiple of 15*");
    }

    [Fact]
    public void Rejects_empty_name_and_id()
    {
        var emptyName = () => new ServiceType(Id, "  ", 30, CertificationType.General);
        var emptyId = () => new ServiceType(Guid.Empty, "Oil", 30, CertificationType.General);

        emptyName.Should().Throw<DomainValidationException>();
        emptyId.Should().Throw<DomainValidationException>();
    }
}
