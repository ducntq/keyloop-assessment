using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly SchedulerDbContext _context;

    public AppointmentRepository(SchedulerDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        await _context.Appointments.AddAsync(appointment, cancellationToken);
    }

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> HasResourceConflictAsync(
        Guid serviceBayId,
        Guid technicianId,
        TimeWindow window,
        Guid? excludeAppointmentId = null,
        CancellationToken cancellationToken = default) =>
        BuildConflictQuery(serviceBayId, technicianId, window, excludeAppointmentId)
            .AnyAsync(cancellationToken);

    public Task<int> CountScheduledAsync(CancellationToken cancellationToken = default) =>
        _context.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Scheduled, cancellationToken);

    /// <summary>
    /// Half-open overlap predicate translated to SQL:
    /// <c>existing.Start &lt; candidate.End AND candidate.Start &lt; existing.End</c>.
    /// Adjacent bookings that merely touch at a boundary do not conflict.
    /// </summary>
    private IQueryable<Appointment> BuildConflictQuery(
        Guid serviceBayId,
        Guid technicianId,
        TimeWindow window,
        Guid? excludeAppointmentId)
    {
        var query = _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.Status == AppointmentStatus.Scheduled &&
                a.StartTimeUtc < window.EndUtc &&
                window.StartUtc < a.EndTimeUtc &&
                (a.ServiceBayId == serviceBayId || a.TechnicianId == technicianId));

        if (excludeAppointmentId is { } excluded)
        {
            query = query.Where(a => a.Id != excluded);
        }

        return query;
    }
}
