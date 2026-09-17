using Application.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Infrastructure.Identity;

/// <summary>
/// Implements Application.Identity.IIdentityService using ASP.NET Core
/// Identity's UserManager/RoleManager (docs/decisions/ADR-004). Never
/// exposes ApplicationUser/ApplicationRole outside this project — callers
/// get back IdentityUserSnapshot.
/// </summary>
public sealed class IdentityServiceImpl(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    GroceryDbContext db) : IIdentityService
{
    public async Task<Guid> CreateUserAsync(Guid storeId, string email, string password, string displayName, IEnumerable<string> roles, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var alreadyExists = await userManager.Users
            .AnyAsync(u => u.StoreId == storeId && u.NormalizedEmail == normalizedEmail, ct);
        if (alreadyExists)
            throw new ConflictAppException($"A user with email '{email}' already exists for this store.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["adminPassword"] = createResult.Errors.Select(e => e.Description).ToArray()
            });

        foreach (var roleName in roles)
        {
            var role = await roleManager.Roles.FirstOrDefaultAsync(r => r.StoreId == storeId && r.Name == roleName, ct)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist for store '{storeId}'. Call EnsureRoleAsync first.");

            // NOT userManager.AddToRoleAsync(user, role.Name) — the default
            // UserStore looks up the role by NormalizedName GLOBALLY
            // internally (IsInRoleAsync does a SingleOrDefaultAsync with no
            // StoreId predicate), which throws once two stores both have an
            // "Admin" role. Inserting the join row directly by RoleId (which
            // IS globally unique) sidesteps that entirely — the same class
            // of bug as the validators, at yet another layer. See
            // docs/operations/troubleshooting.md.
            await db.UserRoles.AddAsync(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = role.Id
            }, ct);
        }

        return user.Id;
    }

    public async Task<IdentityUserSnapshot?> ValidateCredentialsAsync(Guid storeId, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.StoreId == storeId && u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
            return null;

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        return passwordValid ? ToSnapshot(user) : null;
    }

    public async Task<IdentityUserSnapshot?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : ToSnapshot(user);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var roleIds = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);
        if (roleIds.Count == 0)
            return [];

        return await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionKey)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task EnsureRoleAsync(Guid storeId, string roleName, IEnumerable<string> permissions, CancellationToken ct)
    {
        var role = await roleManager.Roles.FirstOrDefaultAsync(r => r.StoreId == storeId && r.Name == roleName, ct);
        if (role is null)
        {
            role = new ApplicationRole(roleName, storeId);
            var createResult = await roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var existingKeys = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionKey)
            .ToListAsync(ct);

        foreach (var permission in permissions.Except(existingKeys))
            await db.RolePermissions.AddAsync(new RolePermissionEntity { RoleId = role.Id, PermissionKey = permission }, ct);
    }

    private static IdentityUserSnapshot ToSnapshot(ApplicationUser user) =>
        new(user.Id, user.StoreId, user.Email!, user.DisplayName, user.IsActive);
}
