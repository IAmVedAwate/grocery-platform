namespace Domain.Notifications;

/// <summary>
/// Generic per-tenant notification (docs/architecture/data-architecture.md
/// Notification schema). ReferenceId points at whatever domain entity the
/// notification is about (e.g. a Product for a low-stock alert) — kept as a
/// plain nullable Guid rather than a typed FK because a notification is
/// deliberately allowed to outlive/reference across different entity types
/// as more Type values are added later; Payload carries the human-readable
/// snapshot for display.
/// </summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Type { get; private set; } = default!;
    public Guid? ReferenceId { get; private set; }
    public string Payload { get; private set; } = default!;
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Notification() { }

    public Notification(Guid storeId, string type, Guid? referenceId, string payload)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Notification type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Notification payload is required.", nameof(payload));

        Id = Guid.NewGuid();
        StoreId = storeId;
        Type = type;
        ReferenceId = referenceId;
        Payload = payload;
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkRead() => IsRead = true;
}

public static class NotificationTypes
{
    public const string LowStock = "LowStock";
}
