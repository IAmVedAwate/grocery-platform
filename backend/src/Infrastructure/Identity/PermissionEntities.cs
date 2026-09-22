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

/// <summary>
/// The authoritative source of what a specific user can do — deliberately
/// NOT derived from RolePermission/role membership at read time. Roles
/// (AspNetUserRoles/RolePermissionEntity) still exist and are used once,
/// as a convenience starting bundle when a user is first created, but
/// after that this table is independently editable per user from Settings
/// (see UserManagementApplicationService) — a real permission checklist,
/// not role-based access control.
/// </summary>
public class UserPermissionEntity
{
    public Guid UserId { get; set; }
    public string PermissionKey { get; set; } = default!;
}
