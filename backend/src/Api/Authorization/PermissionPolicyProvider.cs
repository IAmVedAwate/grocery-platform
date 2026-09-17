using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.Authorization;

/// <summary>
/// Dynamically builds an authorization policy for any "permission:&lt;key&gt;"
/// policy name so each of the ~10 permissions doesn't need to be
/// pre-registered individually at startup — see RequirePermissionAttribute.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return _fallback.GetPolicyAsync(policyName);

        var permission = policyName[PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
