namespace Domain.Catalog;

public class Brand
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private Brand() { }

    public Brand(Guid storeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Brand name is required.", nameof(name));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Name = name.Trim();
        IsActive = true;
    }
}
