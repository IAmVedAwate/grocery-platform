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
}
