namespace Domain.Catalog;

/// <summary>
/// See docs/business/business-rules-catalog.md (Catalog section) for the
/// rules this entity enforces: barcode uniqueness is a database constraint
/// (StoreId, Barcode), not enforced here; everything else that can be
/// checked without hitting the database is enforced in the constructor/
/// mutators so an invalid Product can never exist in memory.
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid? CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? UnitId { get; private set; }
    public decimal Price { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public bool IsActive { get; private set; }
    public int LowStockThreshold { get; private set; }
    public string? ImageStorageKey { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Product() { }

    public Product(
        Guid storeId,
        string sku,
        string name,
        decimal price,
        decimal taxRatePercent,
        string? barcode = null,
        Guid? categoryId = null,
        Guid? brandId = null,
        Guid? unitId = null,
        int lowStockThreshold = 0)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        ValidatePrice(price);
        ValidateTaxRate(taxRatePercent);
        if (lowStockThreshold < 0)
            throw new ArgumentException("Low stock threshold cannot be negative.", nameof(lowStockThreshold));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Sku = sku.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        Name = name.Trim();
        CategoryId = categoryId;
        BrandId = brandId;
        UnitId = unitId;
        Price = price;
        TaxRatePercent = taxRatePercent;
        LowStockThreshold = lowStockThreshold;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void UpdateDetails(
        string name,
        decimal price,
        decimal taxRatePercent,
        string? barcode,
        Guid? categoryId,
        Guid? brandId,
        Guid? unitId,
        int lowStockThreshold)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        ValidatePrice(price);
        ValidateTaxRate(taxRatePercent);
        if (lowStockThreshold < 0)
            throw new ArgumentException("Low stock threshold cannot be negative.", nameof(lowStockThreshold));

        Name = name.Trim();
        Price = price;
        TaxRatePercent = taxRatePercent;
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CategoryId = categoryId;
        BrandId = brandId;
        UnitId = unitId;
        LowStockThreshold = lowStockThreshold;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>The old image (if any) is the caller's responsibility to
    /// delete from storage — Domain has no storage dependency, so it only
    /// ever knows the current key, never what came before it.</summary>
    public void SetImage(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        ImageStorageKey = storageKey;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearImage()
    {
        ImageStorageKey = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidatePrice(decimal price)
    {
        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));
    }

    private static void ValidateTaxRate(decimal taxRatePercent)
    {
        if (taxRatePercent < 0 || taxRatePercent > 100)
            throw new ArgumentException("Tax rate must be between 0 and 100.", nameof(taxRatePercent));
    }
}
