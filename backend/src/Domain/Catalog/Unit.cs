namespace Domain.Catalog;

/// <summary>Unit of measure (e.g. "kg", "pack", "litre"). Named Unit, not
/// UnitOfMeasure, per docs/architecture/data-architecture.md.</summary>
public class Unit
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private Unit() { }

    public Unit(Guid storeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Unit name is required.", nameof(name));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Name = name.Trim();
        IsActive = true;
    }
}
