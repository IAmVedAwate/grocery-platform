namespace Infrastructure.Identity;

/// <summary>The fixed permission catalog as a lookup table (mirrors
/// Domain.Identity.Permissions.All) so RolePermission can carry a real FK
/// rather than an unvalidated string.</summary>
public class PermissionEntity
{
    public string Key { get; set; } = default!;
}

public class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public string PermissionKey { get; set; } = default!;
}
