namespace Application.Common;

/// <summary>
/// Resolved exclusively from the validated JWT's claims by the Api layer —
/// never from a client-supplied header, query string, or body field. This
/// is the enforcement anchor for multi-tenancy (docs/decisions/ADR-002).
/// EF Core's global query filters (Infrastructure) read from this.
/// </summary>
public interface ITenantContext
{
    Guid StoreId { get; }
    Guid UserId { get; }
    bool IsAuthenticated { get; }
}
