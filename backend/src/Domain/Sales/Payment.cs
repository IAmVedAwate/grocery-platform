namespace Domain.Sales;

public enum PaymentMethod { Cash, Card }
public enum PaymentStatus { Captured, Failed }

/// <summary>No real payment gateway in this phase — capture is recorded
/// as an immediate, always-succeeding local status (docs/PRD.md §5.5).</summary>
public class Payment
{
    public Guid Id { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Payment() { }

    internal Payment(Guid salesOrderId, PaymentMethod method, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        Id = Guid.NewGuid();
        SalesOrderId = salesOrderId;
        Method = method;
        Amount = amount;
        Status = PaymentStatus.Captured;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
