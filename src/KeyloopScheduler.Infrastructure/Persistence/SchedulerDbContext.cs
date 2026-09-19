using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work over the scheduler schema. Entity shape is configured
/// through <c>IEntityTypeConfiguration</c> implementations discovered by assembly scan.
/// </summary>
public sealed class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Dealership> Dealerships => Set<Dealership>();

    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();

    public DbSet<Technician> Technicians => Set<Technician>();

    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
