using Domain.Catalog;
using Xunit;

namespace Unit.Catalog;

public class ProductTests
{
    private static readonly Guid StoreId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidData_CreatesActiveProduct()
    {
        var product = new Product(StoreId, "SKU-1", "Test Product", price: 10m, taxRatePercent: 5m);

        Assert.True(product.IsActive);
        Assert.Equal(StoreId, product.StoreId);
        Assert.Equal("SKU-1", product.Sku);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithoutSku_ThrowsArgumentException(string? sku)
    {
        Assert.Throws<ArgumentException>(() => new Product(StoreId, sku!, "Name", 10m, 5m));
    }

    [Fact]
    public void Constructor_WithNegativePrice_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Product(StoreId, "SKU-1", "Name", price: -1m, taxRatePercent: 5m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_WithOutOfRangeTaxRate_ThrowsArgumentException(decimal taxRate)
    {
        Assert.Throws<ArgumentException>(() => new Product(StoreId, "SKU-1", "Name", price: 10m, taxRatePercent: taxRate));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse_AndBumpsUpdatedAt()
    {
        var product = new Product(StoreId, "SKU-1", "Name", 10m, 5m);
        var createdAt = product.UpdatedAtUtc;

        product.Deactivate();

        Assert.False(product.IsActive);
        Assert.True(product.UpdatedAtUtc >= createdAt);
    }

    [Fact]
    public void UpdateDetails_WithNegativePrice_ThrowsArgumentException_AndLeavesProductUnchanged()
    {
        var product = new Product(StoreId, "SKU-1", "Name", 10m, 5m);

        Assert.Throws<ArgumentException>(() =>
            product.UpdateDetails("Name", price: -5m, taxRatePercent: 5m, barcode: null,
                categoryId: null, brandId: null, unitId: null, lowStockThreshold: 0));

        // Domain invariant: a rejected update must not partially apply.
        Assert.Equal(10m, product.Price);
    }
}
