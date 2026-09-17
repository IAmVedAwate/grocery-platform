using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// Replaces ASP.NET Core Identity's default RoleValidator, which checks
/// role-name uniqueness GLOBALLY (across every store) — that breaks
/// multi-tenancy outright, since every store needs its own "Admin" role.
/// Uniqueness here is scoped to (StoreId, NormalizedName) instead, matching
/// docs/architecture/data-architecture.md. See docs/operations/troubleshooting.md
/// for how this was found.
/// </summary>
public sealed class TenantScopedRoleValidator : IRoleValidator<ApplicationRole>
{
    public async Task<IdentityResult> ValidateAsync(RoleManager<ApplicationRole> manager, ApplicationRole role)
    {
        if (string.IsNullOrWhiteSpace(role.Name))
            return IdentityResult.Failed(new IdentityError { Code = "InvalidRoleName", Description = "Role name cannot be empty." });

        var normalized = manager.NormalizeKey(role.Name);
        var duplicate = await manager.Roles.AnyAsync(r =>
            r.StoreId == role.StoreId && r.NormalizedName == normalized && r.Id != role.Id);

        return duplicate
            ? IdentityResult.Failed(new IdentityError { Code = "DuplicateRoleName", Description = $"Role name '{role.Name}' is already taken for this store." })
            : IdentityResult.Success;
    }
}
