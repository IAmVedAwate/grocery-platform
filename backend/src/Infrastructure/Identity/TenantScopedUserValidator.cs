using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// Same fix as TenantScopedRoleValidator, for the same reason: the default
/// UserValidator checks username uniqueness GLOBALLY. Two different stores
/// legitimately having an admin with the same email must both succeed —
/// uniqueness is (StoreId, NormalizedUserName), per
/// docs/architecture/data-architecture.md.
/// </summary>
public sealed class TenantScopedUserValidator : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var errors = new List<IdentityError>();

        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            errors.Add(new IdentityError { Code = "InvalidUserName", Description = "Username cannot be empty." });
        }
        else
        {
            var normalizedUserName = manager.NormalizeName(user.UserName);
            var duplicateUserName = await manager.Users.AnyAsync(u =>
                u.StoreId == user.StoreId && u.NormalizedUserName == normalizedUserName && u.Id != user.Id);
            if (duplicateUserName)
                errors.Add(new IdentityError { Code = "DuplicateUserName", Description = $"Username '{user.UserName}' is already taken for this store." });
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var normalizedEmail = manager.NormalizeEmail(user.Email);
            var duplicateEmail = await manager.Users.AnyAsync(u =>
                u.StoreId == user.StoreId && u.NormalizedEmail == normalizedEmail && u.Id != user.Id);
            if (duplicateEmail)
                errors.Add(new IdentityError { Code = "DuplicateEmail", Description = $"Email '{user.Email}' is already taken for this store." });
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }
}
