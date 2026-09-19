using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.DealershipId).HasColumnName("dealership_id").IsRequired();
        builder.Property(a => a.ServiceBayId).HasColumnName("service_bay_id").IsRequired();
        builder.Property(a => a.TechnicianId).HasColumnName("technician_id").IsRequired();
        builder.Property(a => a.ServiceTypeId).HasColumnName("service_type_id").IsRequired();

        builder.Property(a => a.VehicleIdentification)
            .HasColumnName("vin")
            .HasMaxLength(Vin.StandardLength)
            .HasConversion(v => v.Value, value => new Vin(value))
            .IsRequired();

        // The TimeWindow interval is persisted as two strict-UTC columns so the
        // mandated compound indexes below remain index-friendly for overlap scans.
        builder.Property(a => a.StartTimeUtc)
            .HasColumnName("start_time_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.EndTimeUtc)
            .HasColumnName("end_time_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc")
            .HasColumnType("timestamptz");

        // Domain projections are computed, not stored.
        builder.Ignore(a => a.Window);
        builder.Ignore(a => a.OccupiesResources);

        // Mandated compound indexes: eliminate full table scans during overlap detection.
        builder.HasIndex(a => new { a.ServiceBayId, a.StartTimeUtc, a.EndTimeUtc })
            .HasDatabaseName("ix_appointments_bay_window");

        builder.HasIndex(a => new { a.TechnicianId, a.StartTimeUtc, a.EndTimeUtc })
            .HasDatabaseName("ix_appointments_technician_window");

        builder.HasIndex(a => new { a.DealershipId, a.Status, a.StartTimeUtc })
            .HasDatabaseName("ix_appointments_dealership_status_start");

        builder.HasOne<ServiceBay>()
            .WithMany()
            .HasForeignKey(a => a.ServiceBayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Technician>()
            .WithMany()
            .HasForeignKey(a => a.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ServiceType>()
            .WithMany()
            .HasForeignKey(a => a.ServiceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Dealership>()
            .WithMany()
            .HasForeignKey(a => a.DealershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
