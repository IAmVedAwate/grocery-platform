using Application.Catalog;
using Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Reads rely entirely on GroceryDbContext's Product query filter for
/// tenant scoping (docs/decisions/ADR-002) — no method here takes a
/// StoreId parameter.
/// </summary>
public sealed class ProductRepository(GroceryDbContext db) : IProductRepository
{
    public async Task AddAsync(Product product, CancellationToken ct) => await db.Products.AddAsync(product, ct);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct) =>
        db.Products.AnyAsync(p => p.Barcode == barcode, ct);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(
        int skip, int take, string? search, bool? isActive, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || p.Barcode == search);
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        // Same predicate used for both the count and the page, per
        // docs/api/api-conventions.md — this is the exact mistake class
        // (count query silently diverging from the page's filter) the
        // project's own SQL practice previously surfaced.
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
