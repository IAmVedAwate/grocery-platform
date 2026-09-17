using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// Role assignment is tenant-scoped (docs/PRD.md §13): each store gets its
/// own copy of the default role bundle so a future per-store customization
/// of a role's permissions doesn't affect other stores.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public Guid StoreId { get; set; }

    public ApplicationRole() { }

    public ApplicationRole(string roleName, Guid storeId) : base(roleName)
    {
        Id = Guid.NewGuid();
        StoreId = storeId;
    }
}
