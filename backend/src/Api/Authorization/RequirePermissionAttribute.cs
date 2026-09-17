using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Usage: [RequirePermission(Permissions.CatalogManage)]</summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        // Sets the inherited Policy property directly so it flows through
        // IAuthorizeData.Policy the way the authorization middleware reads
        // it — a `new`-hidden override would NOT be seen there.
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }
}
