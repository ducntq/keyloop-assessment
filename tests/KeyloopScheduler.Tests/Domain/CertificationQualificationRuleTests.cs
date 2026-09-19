using FluentAssertions;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Rules;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: the pluggable certification gate.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CertificationQualificationRuleTests
{
    private readonly CertificationQualificationRule _rule = new();

    private static readonly ServiceType BrakeService =
        new(Guid.Parse("99999999-9999-9999-9999-999999999999"), "Brake Pad Replacement", 60, CertificationType.Brakes);

    private static Technician Technician(params CertificationType[] certifications) =>
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Tech", certifications);

    [Fact]
    public void Qualified_when_certification_requirement_is_met()
    {
        _rule.IsQualified(Technician(CertificationType.General, CertificationType.Brakes), BrakeService)
            .Should().BeTrue();
    }

    [Fact]
    public void Not_qualified_when_required_certification_is_missing()
    {
        _rule.IsQualified(Technician(CertificationType.General), BrakeService)
            .Should().BeFalse();
    }

    [Fact]
    public void Not_qualified_when_technician_is_inactive()
    {
        var technician = Technician(CertificationType.Brakes);
        technician.Deactivate();

        _rule.IsQualified(technician, BrakeService).Should().BeFalse();
    }
}
