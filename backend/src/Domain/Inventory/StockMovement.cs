namespace Domain.Inventory;

public enum StockMovementType
{
    Receipt,
    Sale,
    Adjustment,
    Transfer
}

/// <summary>
/// Append-only (docs/business/business-rules-catalog.md). One row per
/// quantity change to an InventoryItem — never a silent update to
/// QuantityOnHand alone.
/// </summary>
public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int QuantityDelta { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Reason { get; private set; }
    public Guid ActorUserId { get; private set; }
    public int ResultingQuantity { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private StockMovement() { }

    public StockMovement(
        Guid storeId, Guid inventoryItemId, StockMovementType type, int quantityDelta,
        Guid actorUserId, int resultingQuantity,
        string? referenceType = null, Guid? referenceId = null, string? reason = null)
    {
        if (quantityDelta == 0)
            throw new ArgumentException("Quantity delta cannot be zero.", nameof(quantityDelta));
        if (type == StockMovementType.Adjustment && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required for manual adjustments.", nameof(reason));

        Id = Guid.NewGuid();
        StoreId = storeId;
        InventoryItemId = inventoryItemId;
        Type = type;
        QuantityDelta = quantityDelta;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Reason = reason;
        ActorUserId = actorUserId;
        ResultingQuantity = resultingQuantity;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
