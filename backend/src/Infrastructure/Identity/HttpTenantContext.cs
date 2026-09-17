using System.Security.Claims;
using Application.Common;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity;

/// <summary>
/// Resolves the tenant EXCLUSIVELY from the validated JWT's claims
/// (sub, store_id) via HttpContext.User — never from a header, query
/// string, or body field. See docs/architecture/authentication-flow.md
/// "Why the Tenant Boundary Can't Be Spoofed by a Client".
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string StoreIdClaimType = "store_id";

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid StoreId => IsAuthenticated
        ? Guid.Parse(GetRequiredClaim(StoreIdClaimType))
        : Guid.Empty;

    public Guid UserId => IsAuthenticated
        ? Guid.Parse(GetRequiredClaim(ClaimTypes.NameIdentifier))
        : Guid.Empty;

    private string GetRequiredClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException($"Authenticated request is missing the '{claimType}' claim.")
            : value;
    }
}
