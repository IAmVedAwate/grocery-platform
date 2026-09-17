using Domain.Catalog;

namespace Application.Catalog;

/// <summary>
/// Implemented in Infrastructure against GroceryDbContext. Read methods
/// rely on the DbContext's tenant global query filter — callers never pass
/// StoreId explicitly for reads (docs/decisions/ADR-002).
/// </summary>
public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(int skip, int take, string? search, bool? isActive, CancellationToken ct);
}
