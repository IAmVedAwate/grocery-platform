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
    // Hashed once at startup, not per request — computing it on every
    // failed login would cost double the intended work. See the use site
    // in ValidateCredentialsAsync for why this exists at all.
    private static readonly ApplicationUser TimingDecoyUser = new();
    private static readonly string TimingDecoyHash =
        new PasswordHasher<ApplicationUser>().HashPassword(TimingDecoyUser, "timing-equalization-decoy");

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

        var roleIds = new List<Guid>();
        foreach (var roleName in roles)
        {
            var role = await roleManager.Roles.FirstOrDefaultAsync(r => r.StoreId == storeId && r.Name == roleName, ct)
                ?? throw new InvalidOperationException($"Role '{roleName}' does not exist for store '{storeId}'. Call EnsureRoleAsync first.");
            roleIds.Add(role.Id);

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

        // Role membership is only ever used here, once, to seed the
        // starting permission set (this path is exclusively the
        // store-registration admin — see CreateStaffUserAsync for the
        // no-role staff path). From this point on GetPermissionsAsync
        // reads UserPermissionEntity directly; the role assignment above
        // is never re-consulted for authorization.
        if (roleIds.Count > 0)
        {
            var bundlePermissions = await db.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.PermissionKey)
                .Distinct()
                .ToListAsync(ct);
            foreach (var key in bundlePermissions)
                await db.UserPermissions.AddAsync(new UserPermissionEntity { UserId = user.Id, PermissionKey = key }, ct);
        }

        return user.Id;
    }

    public async Task<Guid> CreateStaffUserAsync(Guid storeId, string email, string password, string displayName, IEnumerable<string> permissionKeys, CancellationToken ct)
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
                ["password"] = createResult.Errors.Select(e => e.Description).ToArray()
            });

        foreach (var key in permissionKeys.Distinct())
            await db.UserPermissions.AddAsync(new UserPermissionEntity { UserId = user.Id, PermissionKey = key }, ct);

        return user.Id;
    }

    public async Task<IReadOnlyList<StaffUserSnapshot>> ListStaffAsync(Guid storeId, CancellationToken ct)
    {
        var users = await userManager.Users.Where(u => u.StoreId == storeId).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();
        var permissionsByUser = (await db.UserPermissions
                .Where(up => userIds.Contains(up.UserId))
                .ToListAsync(ct))
            .GroupBy(up => up.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(up => up.PermissionKey).ToList());

        return users
            .Select(u => new StaffUserSnapshot(
                u.Id, u.Email!, u.DisplayName, u.IsActive,
                permissionsByUser.TryGetValue(u.Id, out var perms) ? perms : []))
            .ToList();
    }

    public async Task SetUserPermissionsAsync(Guid userId, IEnumerable<string> permissionKeys, CancellationToken ct)
    {
        var existing = await db.UserPermissions.Where(up => up.UserId == userId).ToListAsync(ct);
        db.UserPermissions.RemoveRange(existing);

        foreach (var key in permissionKeys.Distinct())
            await db.UserPermissions.AddAsync(new UserPermissionEntity { UserId = userId, PermissionKey = key }, ct);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
    }

    public async Task<(Guid UserId, string Token)?> GeneratePasswordResetTokenAsync(Guid storeId, string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.StoreId == storeId && u.NormalizedEmail == normalizedEmail, ct);
        if (user is null || !user.IsActive)
            return null;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return (user.Id, token);
    }

    public async Task<Guid?> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? user.StoreId : null;
    }

    public async Task<IdentityUserSnapshot?> ValidateCredentialsAsync(Guid storeId, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.StoreId == storeId && u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
        {
            // Verify against a throwaway hash before giving up. Password
            // hashing is deliberately expensive (PBKDF2), so returning
            // early here would make an unknown email answer in ~1ms while a
            // wrong password takes ~100ms — response time alone would then
            // reveal which emails are registered, defeating the point of
            // AuthApplicationService returning an identical error for both.
            // The result is discarded; only the elapsed work matters.
            userManager.PasswordHasher.VerifyHashedPassword(TimingDecoyUser, TimingDecoyHash, password);
            return null;
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        return passwordValid ? ToSnapshot(user) : null;
    }

    public async Task<IdentityUserSnapshot?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : ToSnapshot(user);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken ct) =>
        await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.PermissionKey)
            .Distinct()
            .ToListAsync(ct);

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
