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

    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct);

    Task EnsureRoleAsync(Guid storeId, string roleName, IEnumerable<string> permissions, CancellationToken ct);
}

public sealed record IdentityUserSnapshot(Guid UserId, Guid StoreId, string Email, string DisplayName, bool IsActive);
