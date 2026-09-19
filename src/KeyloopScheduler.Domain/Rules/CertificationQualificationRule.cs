using KeyloopScheduler.Domain.Entities;

namespace KeyloopScheduler.Domain.Rules;

/// <summary>
/// Default certification gate: an active technician is qualified when their
/// certification set contains the service type's required certification.
/// </summary>
public sealed class CertificationQualificationRule : ITechnicianQualificationRule
{
    public bool IsQualified(Technician technician, ServiceType serviceType)
    {
        ArgumentNullException.ThrowIfNull(technician);
        ArgumentNullException.ThrowIfNull(serviceType);

        return technician.IsActive && technician.HasCertification(serviceType.RequiredCertification);
    }
}
