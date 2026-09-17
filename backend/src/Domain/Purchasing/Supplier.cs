namespace Domain.Purchasing;

public enum SupplierStatus { Active, Inactive }

public class Supplier
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ContactInfo { get; private set; }
    public SupplierStatus Status { get; private set; }
    public int? PaymentTermsDays { get; private set; }

    private Supplier() { }

    public Supplier(Guid storeId, string name, string? contactInfo = null, int? paymentTermsDays = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name is required.", nameof(name));
        if (paymentTermsDays is < 0)
            throw new ArgumentException("Payment terms cannot be negative.", nameof(paymentTermsDays));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Name = name.Trim();
        ContactInfo = contactInfo;
        PaymentTermsDays = paymentTermsDays;
        Status = SupplierStatus.Active;
    }

    public void Deactivate() => Status = SupplierStatus.Inactive;
    public void Activate() => Status = SupplierStatus.Active;
}
