namespace Domain;

/// <summary>
/// The tenant root. Every tenant-scoped entity elsewhere in the domain
/// carries a StoreId referencing this. See docs/decisions/ADR-002.
/// </summary>
public class Store
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public StoreStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Store() { }

    public Store(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Store name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Store slug is required.", nameof(slug));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Status = StoreStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Suspend() => Status = StoreStatus.Suspended;
}

public enum StoreStatus
{
    Active,
    Suspended
}
