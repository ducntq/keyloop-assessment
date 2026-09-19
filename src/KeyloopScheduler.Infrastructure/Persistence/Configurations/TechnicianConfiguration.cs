using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("technicians");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.DealershipId).HasColumnName("dealership_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();

        // Certification families are stored as a native PostgreSQL integer array so
        // that "does this technician hold certification X" is answered in SQL.
        builder.PrimitiveCollection(t => t.Certifications)
            .HasColumnName("certifications")
            .HasColumnType("integer[]");

        builder.HasIndex(t => new { t.DealershipId, t.IsActive })
            .HasDatabaseName("ix_technicians_dealership_active");

        builder.HasOne<Dealership>()
            .WithMany()
            .HasForeignKey(t => t.DealershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
