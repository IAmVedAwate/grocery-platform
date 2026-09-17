namespace Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    Approved,
    PartiallyReceived,
    Received,
    Cancelled
}

/// <summary>
/// Every purchase order requires explicit approval before it can be
/// received — a simpler, universal gate rather than a value-based
/// threshold (docs/PRD.md §5.4 describes a configurable threshold as a
/// P1 refinement; a fixed approval-required-always rule is the P0
/// baseline and avoids needing a settings entity for Phase 2).
/// </summary>
public class PurchaseOrder
{
    private readonly List<PurchaseOrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid SupplierId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyList<PurchaseOrderItem> Items => _items;

    private PurchaseOrder() { }

    public PurchaseOrder(Guid storeId, Guid supplierId, IEnumerable<(Guid ProductId, int Quantity, decimal UnitCost)> lines)
    {
        Id = Guid.NewGuid();
        StoreId = storeId;
        SupplierId = supplierId;
        Status = PurchaseOrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;

        var lineList = lines.ToList();
        if (lineList.Count == 0)
            throw new ArgumentException("A purchase order must have at least one line item.", nameof(lines));

        foreach (var (productId, quantity, unitCost) in lineList)
            _items.Add(new PurchaseOrderItem(Id, productId, quantity, unitCost));
    }

    public void Submit()
    {
        EnsureStatus(PurchaseOrderStatus.Draft, "submitted");
        Status = PurchaseOrderStatus.Submitted;
    }

    public void Approve(Guid approvedByUserId)
    {
        EnsureStatus(PurchaseOrderStatus.Submitted, "approved");
        Status = PurchaseOrderStatus.Approved;
        ApprovedByUserId = approvedByUserId;
    }

    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a purchase order that is {Status}.");
        Status = PurchaseOrderStatus.Cancelled;
    }

    /// <summary>Applies each (ProductId, Quantity) receipt to its matching
    /// line and advances Status. Quantities are validated per-line by
    /// PurchaseOrderItem.Receive; the caller (PurchasingApplicationService)
    /// is responsible for the corresponding InventoryItem increase — this
    /// method only owns the purchase-order-side state.</summary>
    public void ReceiveItems(IReadOnlyDictionary<Guid, int> quantityByProductId)
    {
        if (Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            throw new InvalidOperationException($"Cannot receive against a purchase order that is {Status}; it must be Approved first.");
        if (quantityByProductId.Count == 0)
            throw new ArgumentException("At least one line must be received.", nameof(quantityByProductId));

        foreach (var (productId, quantity) in quantityByProductId)
        {
            var item = _items.FirstOrDefault(i => i.ProductId == productId)
                ?? throw new InvalidOperationException($"Product '{productId}' is not on this purchase order.");
            item.Receive(quantity);
        }

        Status = _items.All(i => i.IsFullyReceived) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
    }

    private void EnsureStatus(PurchaseOrderStatus required, string action)
    {
        if (Status != required)
            throw new InvalidOperationException($"Purchase order must be {required} to be {action}; it is currently {Status}.");
    }
}
