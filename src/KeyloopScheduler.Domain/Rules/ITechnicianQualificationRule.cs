using KeyloopScheduler.Domain.Entities;

namespace KeyloopScheduler.Domain.Rules;

/// <summary>
/// Policy that decides whether a technician may perform a given service.
/// Implementations can be swapped (or composed) without touching the booking
/// engine, honouring the open/closed principle.
/// </summary>
public interface ITechnicianQualificationRule
{
    bool IsQualified(Technician technician, ServiceType serviceType);
}
