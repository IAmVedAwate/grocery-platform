using Domain.Inventory;

namespace Application.Inventory;

public interface IInventoryRepository
{
    Task AddAsync(InventoryItem item, CancellationToken ct);
    Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken ct);
    Task AddMovementAsync(StockMovement movement, CancellationToken ct);
    Task<(IReadOnlyList<InventoryOverviewRow> Items, int TotalCount)> ListOverviewAsync(int skip, int take, string? search, bool lowStockOnly, CancellationToken ct);
    Task<IReadOnlyList<StockMovement>> GetRecentMovementsAsync(Guid inventoryItemId, int take, CancellationToken ct);
}

/// <summary>Read projection joining InventoryItem with its Product — the
/// shape the inventory list screen actually needs (docs/PRD.md §5.3).</summary>
public sealed record InventoryOverviewRow(
    Guid ProductId, string Sku, string Name, int QuantityOnHand, int LowStockThreshold, bool IsLowStock);
