namespace Domain.Sales;

public class SalesOrderItem
{
    public Guid Id { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal LineDiscount { get; private set; }
    public int QuantityRefunded { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice + TaxAmount - LineDiscount;
    public int RefundableQuantity => Quantity - QuantityRefunded;

    private SalesOrderItem() { }

    internal SalesOrderItem(Guid salesOrderId, Guid productId, int quantity, decimal unitPrice, decimal taxAmount, decimal lineDiscount)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (lineDiscount < 0)
            throw new ArgumentException("Line discount cannot be negative.", nameof(lineDiscount));

        Id = Guid.NewGuid();
        SalesOrderId = salesOrderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        LineDiscount = lineDiscount;
    }

    internal void Refund(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Refund quantity must be positive.", nameof(quantity));
        if (quantity > RefundableQuantity)
            throw new InvalidOperationException(
                $"Cannot refund {quantity}: only {RefundableQuantity} of this line remain refundable.");

        QuantityRefunded += quantity;
    }
}
