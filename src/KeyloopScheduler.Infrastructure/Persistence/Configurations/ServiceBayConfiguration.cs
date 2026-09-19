using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        builder.ToTable("service_bays");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.DealershipId).HasColumnName("dealership_id").IsRequired();
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(b => b.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(b => new { b.DealershipId, b.IsActive })
            .HasDatabaseName("ix_service_bays_dealership_active");

        builder.HasOne<Dealership>()
            .WithMany()
            .HasForeignKey(b => b.DealershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
