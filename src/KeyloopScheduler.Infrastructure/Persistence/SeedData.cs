using KeyloopScheduler.Domain.Enums;

namespace KeyloopScheduler.Infrastructure.Persistence;

/// <summary>
/// Deterministic identifiers for the development seed. Fixed GUIDs let tests and
/// <c>requests.http</c> reference seeded resources without a discovery round-trip.
/// </summary>
public static class SeedData
{
    public static readonly Guid DealershipId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    public static readonly Guid GeneralLiftBayId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    public static readonly Guid AlignmentRackBayId = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    public static readonly Guid EvBayId = Guid.Parse("b0000000-0000-0000-0000-000000000003");

    public static readonly Guid TechAId = Guid.Parse("c0000000-0000-0000-0000-000000000001");

    public static readonly Guid TechBId = Guid.Parse("c0000000-0000-0000-0000-000000000002");

    public static readonly Guid TechCId = Guid.Parse("c0000000-0000-0000-0000-000000000003");

    public static readonly Guid TechDId = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    public static readonly Guid OilAndInspectionServiceTypeId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    public static readonly Guid BrakePadReplacementServiceTypeId = Guid.Parse("d0000000-0000-0000-0000-000000000002");

    public static readonly Guid EvBatteryDiagnosticsServiceTypeId = Guid.Parse("d0000000-0000-0000-0000-000000000003");

    public static readonly Guid CustomerAId = Guid.Parse("e0000000-0000-0000-0000-000000000001");

    public static readonly Guid CustomerBId = Guid.Parse("e0000000-0000-0000-0000-000000000002");

    public const string DealershipName = "Main Dealership";

    public const string CustomerAFullName = "Alice Nguyen";

    public const string CustomerAEmail = "alice.nguyen@example.com";

    public const string CustomerBFullName = "Bob Carter";

    public const string CustomerBEmail = "bob.carter@example.com";

    public static readonly CertificationType[] TechACertifications = { CertificationType.General };

    public static readonly CertificationType[] TechBCertifications =
        { CertificationType.General, CertificationType.Brakes };

    public static readonly CertificationType[] TechCCertifications =
        { CertificationType.EvCertified, CertificationType.General };

    public static readonly CertificationType[] TechDCertifications =
        { CertificationType.Diesel, CertificationType.Brakes };
}
