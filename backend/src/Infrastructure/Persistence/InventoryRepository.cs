using Application.Inventory;
using Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class InventoryRepository(GroceryDbContext db) : IInventoryRepository
{
    public async Task AddAsync(InventoryItem item, CancellationToken ct) => await db.InventoryItems.AddAsync(item, ct);

    public Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken ct) =>
        db.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId, ct);

    public async Task AddMovementAsync(StockMovement movement, CancellationToken ct) =>
        await db.StockMovements.AddAsync(movement, ct);

    public async Task<(IReadOnlyList<InventoryOverviewRow> Items, int TotalCount)> ListOverviewAsync(
        int skip, int take, string? search, bool lowStockOnly, CancellationToken ct)
    {
        // LEFT JOIN — a product with no InventoryItem row yet (never
        // received/adjusted) still shows up with zero stock, rather than
        // being silently absent from the list.
        //
        // Deliberately projects to the final InventoryOverviewRow LAST,
        // after Where/OrderBy/Skip/Take on the raw joined shape — EF Core
        // could not translate OrderBy applied to an already-projected
        // custom record (docs/operations/troubleshooting.md).
        var query =
            from p in db.Products.AsNoTracking()
            join i in db.InventoryItems.AsNoTracking() on p.Id equals i.ProductId into inv
            from item in inv.DefaultIfEmpty()
            select new { Product = p, Item = item };

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Product.Name.Contains(search) || x.Product.Sku.Contains(search));
        if (lowStockOnly)
            query = query.Where(x => (x.Item != null ? x.Item.QuantityOnHand : 0) <= x.Product.LowStockThreshold);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Product.Name)
            .Skip(skip)
            .Take(take)
            .Select(x => new InventoryOverviewRow(
                x.Product.Id, x.Product.Sku, x.Product.Name,
                x.Item != null ? x.Item.QuantityOnHand : 0,
                x.Product.LowStockThreshold,
                (x.Item != null ? x.Item.QuantityOnHand : 0) <= x.Product.LowStockThreshold))
            .ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<StockMovement>> GetRecentMovementsAsync(Guid inventoryItemId, int take, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking()
            .Where(m => m.InventoryItemId == inventoryItemId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
}
