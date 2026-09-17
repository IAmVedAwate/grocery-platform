namespace Domain.Audit;

/// <summary>
/// Append-only (docs/business/business-rules-catalog.md — no Update/Delete
/// exposed above Infrastructure). StoreId is nullable to allow future
/// platform-level events; every event written by application code so far
/// is tenant-scoped.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; private set; }
    public Guid? StoreId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;
    public string? MetadataJson { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(
        Guid? storeId, Guid actorUserId, string action, string entityType, string entityId,
        string? metadataJson = null, string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("EntityId is required.", nameof(entityId));

        Id = Guid.NewGuid();
        StoreId = storeId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        MetadataJson = metadataJson;
        CorrelationId = correlationId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
