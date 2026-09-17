namespace Domain.Sales;

public class Customer
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Customer() { }

    public Customer(Guid storeId, string name, string? phone = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name is required.", nameof(name));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Name = name.Trim();
        Phone = phone;
        Email = email;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
