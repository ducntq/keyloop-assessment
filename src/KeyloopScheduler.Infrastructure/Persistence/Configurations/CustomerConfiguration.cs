using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.DealershipId).HasColumnName("dealership_id").IsRequired();
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(c => new { c.DealershipId, c.IsActive })
            .HasDatabaseName("ix_customers_dealership_active");

        builder.HasOne<Dealership>()
            .WithMany()
            .HasForeignKey(c => c.DealershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
