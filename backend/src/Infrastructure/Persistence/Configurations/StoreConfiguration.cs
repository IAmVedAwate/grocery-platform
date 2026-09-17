using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(s => s.Slug).IsUnique();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
    }
}
