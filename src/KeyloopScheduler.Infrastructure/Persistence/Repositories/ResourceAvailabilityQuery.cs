using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Abstractions.Models;
using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;
using KeyloopScheduler.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Infrastructure.Persistence.Repositories;

internal sealed class ResourceAvailabilityQuery : IResourceAvailabilityQuery
{
    private readonly SchedulerDbContext _context;
    private readonly ITechnicianQualificationRule _qualificationRule;

    public ResourceAvailabilityQuery(
        SchedulerDbContext context,
        ITechnicianQualificationRule qualificationRule)
    {
        _context = context;
        _qualificationRule = qualificationRule;
    }

    public async Task<bool> IsServiceBayAvailableAsync(
        Guid serviceBayId,
        TimeWindow window,
        CancellationToken cancellationToken = default) =>
        !await _context.Appointments
            .AsNoTracking()
            .AnyAsync(
                a => a.Status == AppointmentStatus.Scheduled &&
                     a.ServiceBayId == serviceBayId &&
                     a.StartTimeUtc < window.EndUtc &&
                     window.StartUtc < a.EndTimeUtc,
                cancellationToken);

    public async Task<bool> IsTechnicianAvailableAsync(
        Guid technicianId,
        TimeWindow window,
        CancellationToken cancellationToken = default) =>
        !await _context.Appointments
            .AsNoTracking()
            .AnyAsync(
                a => a.Status == AppointmentStatus.Scheduled &&
                     a.TechnicianId == technicianId &&
                     a.StartTimeUtc < window.EndUtc &&
                     window.StartUtc < a.EndTimeUtc,
                cancellationToken);

    public async Task<IReadOnlyList<AvailabilitySlot>> FindAvailableSlotsAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var serviceType = await _context.ServiceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.ServiceTypeId, cancellationToken)
            ?? throw EntityNotFoundException.For("ServiceType", query.ServiceTypeId);

        var bays = await _context.ServiceBays
            .AsNoTracking()
            .Where(b => b.DealershipId == query.DealershipId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var technicians = await _context.Technicians
            .AsNoTracking()
            .Where(t => t.DealershipId == query.DealershipId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // The certification gate is a pluggable domain rule, not an ad-hoc predicate.
        var qualifiedTechnicians = technicians
            .Where(t => _qualificationRule.IsQualified(t, serviceType))
            .ToList();

        if (bays.Count == 0 || qualifiedTechnicians.Count == 0)
        {
            return Array.Empty<AvailabilitySlot>();
        }

        var bayIds = bays.Select(b => b.Id).ToList();
        var technicianIds = qualifiedTechnicians.Select(t => t.Id).ToList();

        var dayStart = query.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        // One query for the whole day of occupancy; overlap arithmetic happens in memory
        // over a tiny, already-filtered set (never a full-table scan).
        var booked = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled &&
                        a.StartTimeUtc < dayEnd &&
                        dayStart < a.EndTimeUtc &&
                        (bayIds.Contains(a.ServiceBayId) || technicianIds.Contains(a.TechnicianId)))
            .Select(a => new BookedInterval(a.ServiceBayId, a.TechnicianId, a.StartTimeUtc, a.EndTimeUtc))
            .ToListAsync(cancellationToken);

        var open = dayStart.Add(BusinessHours.OpenUtc);
        var close = dayStart.Add(BusinessHours.CloseUtc);
        var step = TimeSpan.FromMinutes(BusinessHours.QuantizationMinutes);
        var duration = serviceType.Duration;

        var slots = new List<AvailabilitySlot>();

        for (var start = open; start + duration <= close; start = start.Add(step))
        {
            var end = start + duration;

            foreach (var bay in bays)
            {
                if (IsResourceBooked(booked, b => b.ServiceBayId == bay.Id, start, end))
                {
                    continue;
                }

                foreach (var technician in qualifiedTechnicians)
                {
                    if (IsResourceBooked(booked, b => b.TechnicianId == technician.Id, start, end))
                    {
                        continue;
                    }

                    slots.Add(new AvailabilitySlot(bay.Id, technician.Id, start, end));
                }
            }
        }

        return slots;
    }

    private static bool IsResourceBooked(
        List<BookedInterval> booked,
        Func<BookedInterval, bool> resourceMatch,
        DateTime startUtc,
        DateTime endUtc) =>
        booked.Any(b => resourceMatch(b) && b.StartTimeUtc < endUtc && startUtc < b.EndTimeUtc);

    private sealed record BookedInterval(
        Guid ServiceBayId,
        Guid TechnicianId,
        DateTime StartTimeUtc,
        DateTime EndTimeUtc);
}
