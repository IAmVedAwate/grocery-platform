namespace Domain.Sales;

public enum SalesOrderStatus { Completed, PartiallyRefunded, Refunded }

/// <summary>
/// Checkout is synchronous and atomic in this phase (docs/PRD.md §22) —
/// there is no persisted "Draft/cart" state; by the time a SalesOrder is
/// constructed, stock has already been validated and the order is
/// complete. Price/tax/discount are computed server-side from the
/// product's current configuration by the caller
/// (SalesApplicationService) and passed in already resolved — this type
/// does not re-derive them, it only enforces the invariants that don't
/// depend on external state (docs/business/business-rules-catalog.md).
/// </summary>
public class SalesOrder
{
    private readonly List<SalesOrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<SalesOrderItem> Items => _items;
    public Payment? Payment { get; private set; }
    public Invoice? Invoice { get; private set; }

    private SalesOrder() { }

    public SalesOrder(
        Guid storeId, Guid? customerId, Guid createdByUserId, string idempotencyKey,
        IEnumerable<(Guid ProductId, int Quantity, decimal UnitPrice, decimal TaxAmount, decimal LineDiscount)> lines)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        Id = Guid.NewGuid();
        StoreId = storeId;
        CustomerId = customerId;
        CreatedByUserId = createdByUserId;
        IdempotencyKey = idempotencyKey;
        Status = SalesOrderStatus.Completed;
        CreatedAtUtc = DateTime.UtcNow;

        var lineList = lines.ToList();
        if (lineList.Count == 0)
            throw new ArgumentException("A sale must have at least one line item.", nameof(lines));

        foreach (var line in lineList)
            _items.Add(new SalesOrderItem(Id, line.ProductId, line.Quantity, line.UnitPrice, line.TaxAmount, line.LineDiscount));

        SubtotalAmount = _items.Sum(i => i.Quantity * i.UnitPrice);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        DiscountAmount = _items.Sum(i => i.LineDiscount);
        TotalAmount = SubtotalAmount + TaxAmount - DiscountAmount;
    }

    public Payment CapturePayment(PaymentMethod method)
    {
        if (Payment is not null)
            throw new InvalidOperationException("This sale already has a captured payment.");

        Payment = new Payment(Id, method, TotalAmount);
        return Payment;
    }

    public Invoice IssueInvoice(string invoiceNumber)
    {
        if (Invoice is not null)
            throw new InvalidOperationException("This sale already has an invoice.");

        Invoice = new Invoice(Id, invoiceNumber);
        return Invoice;
    }

    /// <summary>Refund must reference real lines on THIS sale and cannot
    /// exceed originally sold (minus already refunded) quantity per line
    /// (docs/business/business-rules-catalog.md). Restocking is the
    /// caller's responsibility (SalesApplicationService, via
    /// InventoryApplicationService) — this only owns sale-side state.</summary>
    public void RefundLines(IReadOnlyDictionary<Guid, int> quantityByProductId)
    {
        if (quantityByProductId.Count == 0)
            throw new ArgumentException("At least one line must be refunded.", nameof(quantityByProductId));

        foreach (var (productId, quantity) in quantityByProductId)
        {
            var item = _items.FirstOrDefault(i => i.ProductId == productId)
                ?? throw new InvalidOperationException($"Product '{productId}' is not on this sale.");
            item.Refund(quantity);
        }

        Status = _items.All(i => i.RefundableQuantity == 0) ? SalesOrderStatus.Refunded : SalesOrderStatus.PartiallyRefunded;
    }
}
