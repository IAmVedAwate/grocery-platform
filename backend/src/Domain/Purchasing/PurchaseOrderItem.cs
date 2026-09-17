namespace Domain.Purchasing;

public class PurchaseOrderItem
{
    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityOrdered { get; private set; }
    public decimal UnitCost { get; private set; }
    public int QuantityReceived { get; private set; }

    public bool IsFullyReceived => QuantityReceived >= QuantityOrdered;

    private PurchaseOrderItem() { }

    internal PurchaseOrderItem(Guid purchaseOrderId, Guid productId, int quantityOrdered, decimal unitCost)
    {
        if (quantityOrdered <= 0)
            throw new ArgumentException("Quantity ordered must be positive.", nameof(quantityOrdered));
        if (unitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        Id = Guid.NewGuid();
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        QuantityOrdered = quantityOrdered;
        UnitCost = unitCost;
    }

    /// <summary>Receiving cannot exceed the ordered quantity unless
    /// explicitly allowed — it isn't, by default
    /// (docs/business/business-rules-catalog.md).</summary>
    internal void Receive(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity to receive must be positive.", nameof(quantity));
        if (QuantityReceived + quantity > QuantityOrdered)
            throw new InvalidOperationException(
                $"Cannot receive {quantity}: only {QuantityOrdered - QuantityReceived} remain on this line.");

        QuantityReceived += quantity;
    }
}
