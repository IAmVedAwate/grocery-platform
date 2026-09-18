namespace Application.Notifications;

/// <summary>
/// Implemented in Infrastructure — generation has to scan every tenant's
/// InventoryItem/Product rows in one pass and cannot go through the normal
/// tenant-scoped repositories, because the background worker that calls
/// this has no HttpContext and therefore no resolvable ITenantContext (see
/// the implementation's own remarks and docs/operations/troubleshooting.md).
/// </summary>
public interface ILowStockNotificationGenerator
{
    /// <returns>How many new notifications were created this sweep.</returns>
    Task<int> GenerateAsync(CancellationToken ct);
}
