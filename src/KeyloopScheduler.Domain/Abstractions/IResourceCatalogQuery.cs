using KeyloopScheduler.Domain.Entities;

namespace KeyloopScheduler.Domain.Abstractions;

/// <summary>
/// Read-only access to the static resource catalogue. Segregated from the
/// booking command surface so read traffic can be scaled independently.
/// </summary>
public interface IResourceCatalogQuery
{
    Task<bool> DealershipExistsAsync(Guid dealershipId, CancellationToken cancellationToken = default);

    Task<ServiceType?> GetServiceTypeAsync(Guid serviceTypeId, CancellationToken cancellationToken = default);

    Task<ServiceBay?> GetServiceBayAsync(Guid serviceBayId, CancellationToken cancellationToken = default);

    Task<Technician?> GetTechnicianAsync(Guid technicianId, CancellationToken cancellationToken = default);

    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceBay>> GetActiveServiceBaysAsync(
        Guid dealershipId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Technician>> GetActiveTechniciansAsync(
        Guid dealershipId,
        CancellationToken cancellationToken = default);
}
