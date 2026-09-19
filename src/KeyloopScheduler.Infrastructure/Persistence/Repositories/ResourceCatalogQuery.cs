using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Infrastructure.Persistence.Repositories;

internal sealed class ResourceCatalogQuery : IResourceCatalogQuery
{
    private readonly SchedulerDbContext _context;

    public ResourceCatalogQuery(SchedulerDbContext context)
    {
        _context = context;
    }

    public Task<bool> DealershipExistsAsync(Guid dealershipId, CancellationToken cancellationToken = default) =>
        _context.Dealerships.AnyAsync(d => d.Id == dealershipId, cancellationToken);

    public Task<ServiceType?> GetServiceTypeAsync(Guid serviceTypeId, CancellationToken cancellationToken = default) =>
        _context.ServiceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serviceTypeId, cancellationToken);

    public Task<ServiceBay?> GetServiceBayAsync(Guid serviceBayId, CancellationToken cancellationToken = default) =>
        _context.ServiceBays
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == serviceBayId, cancellationToken);

    public Task<Technician?> GetTechnicianAsync(Guid technicianId, CancellationToken cancellationToken = default) =>
        _context.Technicians
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken);

    public async Task<IReadOnlyList<ServiceBay>> GetActiveServiceBaysAsync(
        Guid dealershipId,
        CancellationToken cancellationToken = default) =>
        await _context.ServiceBays
            .AsNoTracking()
            .Where(b => b.DealershipId == dealershipId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Technician>> GetActiveTechniciansAsync(
        Guid dealershipId,
        CancellationToken cancellationToken = default) =>
        await _context.Technicians
            .AsNoTracking()
            .Where(t => t.DealershipId == dealershipId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
}
