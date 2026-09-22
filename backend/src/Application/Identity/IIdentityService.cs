namespace Application.Identity;

/// <summary>
/// Application-facing abstraction over credential storage/verification.
/// Implemented in Infrastructure using ASP.NET Core Identity's
/// UserManager/SignInManager (docs/decisions/ADR-004) — this interface
/// exists so Application never references the Infrastructure-owned
/// ApplicationUser/ApplicationRole types directly.
/// </summary>
public interface IIdentityService
{
    Task<Guid> CreateUserAsync(Guid storeId, string email, string password, string displayName, IEnumerable<string> roles, CancellationToken ct);

    Task<IdentityUserSnapshot?> ValidateCredentialsAsync(Guid storeId, string email, string password, CancellationToken ct);

    Task<IdentityUserSnapshot?> GetUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// The user's actual, currently-effective permission set — read from
    /// per-user assignments, not derived from role membership (see
    /// UserPermissionEntity). Called at both login and refresh, so an
    /// admin editing a staff member's permissions takes effect on that
    /// user's next token refresh, without needing a separate revocation
    /// mechanism.
    /// </summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct);

    Task EnsureRoleAsync(Guid storeId, string roleName, IEnumerable<string> permissions, CancellationToken ct);

    /// <summary>
    /// Creates a staff user with an explicit, directly-assigned permission
    /// set — no role is involved at all (this is the "not RBAC" path;
    /// CreateUserAsync/roles remain only for the store-registration admin).
    /// </summary>
    Task<Guid> CreateStaffUserAsync(Guid storeId, string email, string password, string displayName, IEnumerable<string> permissionKeys, CancellationToken ct);

    Task<IReadOnlyList<StaffUserSnapshot>> ListStaffAsync(Guid storeId, CancellationToken ct);

    /// <summary>Replaces a user's entire permission set (not additive).</summary>
    Task SetUserPermissionsAsync(Guid userId, IEnumerable<string> permissionKeys, CancellationToken ct);

    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken ct);
}

public sealed record IdentityUserSnapshot(Guid UserId, Guid StoreId, string Email, string DisplayName, bool IsActive);

public sealed record StaffUserSnapshot(Guid UserId, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Permissions);
