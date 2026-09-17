using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// The persistence-layer representation of a user, owned by Infrastructure
/// because it is inherently coupled to ASP.NET Core Identity
/// (docs/decisions/ADR-004). Application/Domain never reference this type
/// directly — see Application.Identity.IIdentityService and
/// IdentityUserSnapshot.
///
/// Deliberately has NO EF Core global query filter: login must look up a
/// user by an explicit (StoreId, Email) pair supplied in the request
/// before any JWT/tenant claim exists, so the tenant boundary here is
/// enforced by explicit predicates in IdentityService, not the ambient
/// ITenantContext.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid StoreId { get; set; }
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
