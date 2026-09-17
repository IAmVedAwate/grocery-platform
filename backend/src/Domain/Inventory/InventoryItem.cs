namespace Domain.Inventory;

/// <summary>
/// One row per (StoreId, ProductId) — single-location in this phase
/// (docs/architecture/data-architecture.md "What Is Deliberately Not
/// Modeled Yet"). RowVersion is a SQL Server ROWVERSION column
/// (Infrastructure config), giving optimistic concurrency for free: two
/// concurrent Decrease calls racing for the last unit resolve to exactly
/// one winner at SaveChanges time, never a negative balance
/// (docs/decisions — see the concurrency scenario in docs/PRD.md §23).
/// </summary>
public class InventoryItem
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public DateTime UpdatedAtUtc { get; private set; }

    private InventoryItem() { }

    public InventoryItem(Guid storeId, Guid productId, int initialQuantity = 0)
    {
        if (initialQuantity < 0)
            throw new ArgumentException("Initial quantity cannot be negative.", nameof(initialQuantity));

        Id = Guid.NewGuid();
        StoreId = storeId;
        ProductId = productId;
        QuantityOnHand = initialQuantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Increase(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity to increase must be positive.", nameof(quantity));

        QuantityOnHand += quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Safety-net check — the Application layer checks availability before
    /// calling this too (so it can raise a clean 409 rather than an
    /// unhandled exception), but an invalid state must never be
    /// reachable through this type alone (docs/business/business-rules-catalog.md:
    /// "stock quantity can never go negative").
    /// </summary>
    public void Decrease(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity to decrease must be positive.", nameof(quantity));
        if (QuantityOnHand < quantity)
            throw new InvalidOperationException($"Insufficient stock: {QuantityOnHand} available, {quantity} requested.");

        QuantityOnHand -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
