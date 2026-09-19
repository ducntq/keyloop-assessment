using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("service_types");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(s => s.RequiredCertification)
            .HasColumnName("required_certification")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Derived from DurationMinutes; never persisted twice.
        builder.Ignore(s => s.Duration);
    }
}
