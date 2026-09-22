using Application.Common;
using Domain.Identity;
using Shared.Exceptions;

namespace Application.Identity;

/// <summary>
/// Staff account + permission management (docs/PRD.md §5.1, the
/// "invite my staff" feature). Deliberately not role-based at the
/// authorization boundary: a store owner picks an explicit set of
/// permission keys per person rather than choosing from a fixed role
/// list — see UserPermissionEntity's remarks for why.
/// </summary>
public sealed class UserManagementApplicationService(
    IIdentityService identityService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IAuditWriter auditWriter)
{
    public async Task<Guid> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = ["Email is required."];
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors["displayName"] = ["Name is required."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters."];

        var invalidKeys = request.Permissions.Except(Permissions.All).ToList();
        if (invalidKeys.Count > 0)
            errors["permissions"] = [$"Unknown permission key(s): {string.Join(", ", invalidKeys)}"];

        if (errors.Count > 0)
            throw new ValidationAppException(errors);

        var userId = await identityService.CreateStaffUserAsync(
            tenantContext.StoreId, request.Email, request.Password, request.DisplayName, request.Permissions, ct);

        auditWriter.Record("user.created", "ApplicationUser", userId.ToString(),
            new { request.Email, request.DisplayName, request.Permissions });

        await unitOfWork.SaveChangesAsync(ct);
        return userId;
    }

    public Task<IReadOnlyList<StaffUserSnapshot>> ListStaffAsync(CancellationToken ct) =>
        identityService.ListStaffAsync(tenantContext.StoreId, ct);

    public async Task UpdatePermissionsAsync(Guid userId, IReadOnlyList<string> permissionKeys, CancellationToken ct)
    {
        await EnsureUserBelongsToCurrentStoreAsync(userId, ct);

        var invalidKeys = permissionKeys.Except(Permissions.All).ToList();
        if (invalidKeys.Count > 0)
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["permissions"] = [$"Unknown permission key(s): {string.Join(", ", invalidKeys)}"]
            });

        await identityService.SetUserPermissionsAsync(userId, permissionKeys, ct);

        auditWriter.Record("user.permissions_changed", "ApplicationUser", userId.ToString(), new { permissions = permissionKeys });

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        await EnsureUserBelongsToCurrentStoreAsync(userId, ct);

        if (!isActive && userId == tenantContext.UserId)
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["userId"] = ["You cannot deactivate your own account."]
            });

        await identityService.SetActiveAsync(userId, isActive, ct);

        auditWriter.Record(isActive ? "user.activated" : "user.deactivated", "ApplicationUser", userId.ToString());

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureUserBelongsToCurrentStoreAsync(Guid userId, CancellationToken ct)
    {
        var user = await identityService.GetUserAsync(userId, ct);
        // Same "404, never a 403 that leaks existence across tenants"
        // convention as every other tenant-scoped lookup in this system
        // (docs/PRD.md §11) — a store admin passing another store's
        // userId learns nothing from the response.
        if (user is null || user.StoreId != tenantContext.StoreId)
            throw new NotFoundException("User", userId);
    }
}

public sealed record CreateStaffRequest(string Email, string Password, string DisplayName, IReadOnlyList<string> Permissions);
