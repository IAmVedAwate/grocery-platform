using Application.Common;
using Application.Inventory;
using Domain.Purchasing;
using Shared.Exceptions;

namespace Application.Purchasing;

public sealed class PurchasingApplicationService(
    IPurchaseOrderRepository purchaseOrders,
    ISupplierRepository suppliers,
    InventoryApplicationService inventoryService,
    ITenantContext tenantContext,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
{
    public async Task<PurchaseOrder> CreateAsync(Guid supplierId, IReadOnlyList<CreatePurchaseOrderLine> lines, CancellationToken ct)
    {
        _ = await suppliers.GetByIdAsync(supplierId, ct) ?? throw new NotFoundException(nameof(Domain.Purchasing.Supplier), supplierId);

        var order = new PurchaseOrder(tenantContext.StoreId, supplierId,
            lines.Select(l => (l.ProductId, l.Quantity, l.UnitCost)));

        await purchaseOrders.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return order;
    }

    public async Task<PurchaseOrder> SubmitAsync(Guid orderId, CancellationToken ct)
    {
        var order = await GetOrderAsync(orderId, ct);
        DomainRuleGuard.Run(order.Submit);
        await unitOfWork.SaveChangesAsync(ct);
        return order;
    }

    public async Task<PurchaseOrder> ApproveAsync(Guid orderId, CancellationToken ct)
    {
        var order = await GetOrderAsync(orderId, ct);
        DomainRuleGuard.Run(() => order.Approve(tenantContext.UserId));

        auditWriter.Record("purchase.approved", nameof(PurchaseOrder), order.Id.ToString(),
            new { supplierId = order.SupplierId });

        await unitOfWork.SaveChangesAsync(ct);
        return order;
    }

    public async Task<PurchaseOrder> CancelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await GetOrderAsync(orderId, ct);
        DomainRuleGuard.Run(order.Cancel);
        await unitOfWork.SaveChangesAsync(ct);
        return order;
    }

    /// <summary>The purchase-order side (PurchaseOrderItem.QuantityReceived,
    /// Status) and the inventory side (InventoryItem.QuantityOnHand, a new
    /// StockMovement per line) commit in ONE SaveChanges call — a receipt
    /// can't update stock without also updating the PO, or vice versa.</summary>
    public async Task<PurchaseOrder> ReceiveAsync(Guid orderId, IReadOnlyDictionary<Guid, int> quantityByProductId, CancellationToken ct)
    {
        var order = await GetOrderAsync(orderId, ct);
        DomainRuleGuard.Run(() => order.ReceiveItems(quantityByProductId));

        foreach (var (productId, quantity) in quantityByProductId)
            await inventoryService.ReceiveAsync(productId, quantity, nameof(PurchaseOrder), order.Id, ct);

        auditWriter.Record("purchase.received", nameof(PurchaseOrder), order.Id.ToString(),
            new { lines = quantityByProductId, resultingStatus = order.Status.ToString() });

        await unitOfWork.SaveChangesAsync(ct);
        return order;
    }

    public async Task<PurchaseOrder> GetAsync(Guid orderId, CancellationToken ct) => await GetOrderAsync(orderId, ct);

    public async Task<PagedResult<PurchaseOrder>> ListAsync(PageRequest page, PurchaseOrderStatus? status, CancellationToken ct)
    {
        var (items, totalCount) = await purchaseOrders.ListAsync(page.Skip, page.PageSize, status, ct);
        return new PagedResult<PurchaseOrder> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    private async Task<PurchaseOrder> GetOrderAsync(Guid orderId, CancellationToken ct) =>
        await purchaseOrders.GetByIdAsync(orderId, ct) ?? throw new NotFoundException(nameof(PurchaseOrder), orderId);
}

public sealed record CreatePurchaseOrderLine(Guid ProductId, int Quantity, decimal UnitCost);
