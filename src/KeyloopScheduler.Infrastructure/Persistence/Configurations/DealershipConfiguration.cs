using KeyloopScheduler.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeyloopScheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("dealerships");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
    }
}
