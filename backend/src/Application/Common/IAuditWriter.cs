namespace Application.Common;

/// <summary>
/// Stages an audit entry on the current unit of work — does not save on
/// its own, so it commits atomically with whatever else the calling use
/// case is doing (docs/PRD.md §28 Auditability). CorrelationId comes from
/// the current request; ActorUserId and StoreId come from ITenantContext.
/// </summary>
public interface IAuditWriter
{
    void Record(string action, string entityType, string entityId, object? metadata = null);

    /// <summary>
    /// For the one action that happens BEFORE a tenant/actor is
    /// resolvable from the request (a successful login itself — there is
    /// no JWT on the incoming login request, so ITenantContext.IsAuthenticated
    /// is false and StoreId/UserId can't be inferred the normal way).
    /// Failed login attempts stay Serilog-only (AuthApplicationService),
    /// not written here — an unauthenticated caller can trigger arbitrarily
    /// many failed attempts (the rate limiter bounds the rate, not the
    /// eventual count), and that's log volume, not an audit-worthy event
    /// tied to a real actor.
    /// </summary>
    void RecordWithExplicitActor(Guid storeId, Guid actorUserId, string action, string entityType, string entityId, object? metadata = null);
}
