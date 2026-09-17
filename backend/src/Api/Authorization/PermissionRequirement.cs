using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>
/// docs/decisions/ADR-004: permission-string policies, never role-string
/// comparisons in controller code.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
