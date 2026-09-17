using Application.Common;
using Domain.Inventory;
using Shared.Exceptions;

namespace Application.Inventory;

/// <summary>
/// Owns every stock quantity change in the system — Purchasing (receiving)
/// and Sales (checkout) call into this rather than touching InventoryItem
/// directly, so "every quantity change produces a StockMovement" is
/// enforced in one place (docs/business/business-rules-catalog.md).
///
/// Deliberately does not call IUnitOfWork.SaveChangesAsync — callers
/// (PurchasingApplicationService, SalesApplicationService) commit once,
/// atomically, alongside whatever else their use case changed.
/// </summary>
public sealed class InventoryApplicationService(
    IInventoryRepository inventory,
    ITenantContext tenantContext,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
{
    public async Task<InventoryItem> GetOrCreateAsync(Guid productId, CancellationToken ct)
    {
        var item = await inventory.GetByProductIdAsync(productId, ct);
        if (item is not null) return item;

        item = new InventoryItem(tenantContext.StoreId, productId);
        await inventory.AddAsync(item, ct);
        return item;
    }

    public async Task ReceiveAsync(Guid productId, int quantity, string referenceType, Guid referenceId, CancellationToken ct)
    {
        var item = await GetOrCreateAsync(productId, ct);
        item.Increase(quantity);
        await inventory.AddMovementAsync(
            new StockMovement(tenantContext.StoreId, item.Id, StockMovementType.Receipt, quantity,
                tenantContext.UserId, item.QuantityOnHand, referenceType, referenceId),
            ct);
    }

    /// <summary>Throws ConflictAppException (409) on insufficient stock —
    /// the pre-check a caller wants before committing to a sale. The real
    /// safety net against a *concurrent* sale racing this one is
    /// InventoryItem's RowVersion, checked at SaveChanges time by whoever
    /// calls this (docs/architecture/data-architecture.md Concurrency
    /// Design) — this method only guards against the non-concurrent,
    /// already-insufficient case.</summary>
    public async Task SellAsync(Guid productId, int quantity, string referenceType, Guid referenceId, CancellationToken ct)
    {
        var item = await inventory.GetByProductIdAsync(productId, ct)
            ?? throw new ConflictAppException($"No stock on hand for product '{productId}'.");

        if (item.QuantityOnHand < quantity)
            throw new ConflictAppException(
                $"Insufficient stock for product '{productId}': {item.QuantityOnHand} available, {quantity} requested.");

        item.Decrease(quantity);
        await inventory.AddMovementAsync(
            new StockMovement(tenantContext.StoreId, item.Id, StockMovementType.Sale, -quantity,
                tenantContext.UserId, item.QuantityOnHand, referenceType, referenceId),
            ct);
    }

    /// <summary>Manual adjustment is a standalone, auditable action
    /// (docs/PRD.md §28) — unlike Receive/Sell, it owns its own
    /// transaction rather than being composed into a larger one.</summary>
    public async Task<InventoryItem> AdjustAsync(Guid productId, int quantityDelta, string reason, CancellationToken ct)
    {
        if (quantityDelta == 0)
            throw new ValidationAppException(new Dictionary<string, string[]> { ["quantityDelta"] = ["Adjustment cannot be zero."] });

        var item = await GetOrCreateAsync(productId, ct);
        DomainRuleGuard.Run(() =>
        {
            if (quantityDelta > 0) item.Increase(quantityDelta);
            else item.Decrease(-quantityDelta);
        });

        await inventory.AddMovementAsync(
            new StockMovement(tenantContext.StoreId, item.Id, StockMovementType.Adjustment, quantityDelta,
                tenantContext.UserId, item.QuantityOnHand, reason: reason),
            ct);

        auditWriter.Record("inventory.adjusted", nameof(InventoryItem), item.Id.ToString(),
            new { productId, quantityDelta, reason, resultingQuantity = item.QuantityOnHand });

        await unitOfWork.SaveChangesAsync(ct);
        return item;
    }

    public Task<(IReadOnlyList<InventoryOverviewRow> Items, int TotalCount)> ListOverviewAsync(
        PageRequest page, string? search, bool lowStockOnly, CancellationToken ct) =>
        inventory.ListOverviewAsync(page.Skip, page.PageSize, search, lowStockOnly, ct);

    /// <summary>A product that's never been received/adjusted/sold simply
    /// has no history yet — that's a valid empty state, not a 404 (the
    /// product itself may well exist).</summary>
    public async Task<IReadOnlyList<StockMovement>> GetHistoryAsync(Guid productId, CancellationToken ct)
    {
        var item = await inventory.GetByProductIdAsync(productId, ct);
        return item is null ? [] : await inventory.GetRecentMovementsAsync(item.Id, 50, ct);
    }
}
