namespace Domain.Sales;

public class Invoice
{
    public Guid Id { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public DateTime IssuedAtUtc { get; private set; }

    private Invoice() { }

    internal Invoice(Guid salesOrderId, string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));

        Id = Guid.NewGuid();
        SalesOrderId = salesOrderId;
        InvoiceNumber = invoiceNumber;
        IssuedAtUtc = DateTime.UtcNow;
    }
}
