using Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// Deliberately does NOT set the tenant query filter here — see the long
/// comment on GroceryDbContext.OnModelCreating for why that filter must be
/// written inline against the DbContext's own captured tenantContext
/// field, not from a separate configuration class instance.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(64);
        builder.Property(p => p.Barcode).HasMaxLength(64);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        builder.Property(p => p.TaxRatePercent).HasColumnType("decimal(5,2)");
        builder.Property(p => p.ImageStorageKey).HasMaxLength(128);
        builder.Property(p => p.DominantColorHex).HasMaxLength(7);

        builder.HasIndex(p => new { p.StoreId, p.Sku }).IsUnique();
        builder.HasIndex(p => new { p.StoreId, p.Barcode }).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        builder.HasIndex(p => new { p.StoreId, p.IsActive, p.Name });
    }
}
