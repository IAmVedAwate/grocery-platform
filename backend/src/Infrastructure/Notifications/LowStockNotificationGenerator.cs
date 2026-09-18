using System.Text.Json;
using Application.Notifications;
using Domain.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Notifications;

/// <summary>
/// Runs from a background worker with no HttpContext, so GroceryDbContext's
/// tenant global query filters — which read ITenantContext.StoreId, itself
/// resolved from the current HTTP request's JWT claims (HttpTenantContext)
/// — cannot be relied on here: outside a request, ITenantContext.StoreId is
/// Guid.Empty, so every normal tenant-scoped repository call would just
/// silently return nothing for every store. This has to be a genuine
/// cross-tenant scan (IgnoreQueryFilters on every side of the join) that
/// groups by StoreId itself, then writes each new Notification with its
/// StoreId set explicitly — a documented, deliberate exception to
/// "Application code never sees StoreId" (docs/decisions/ADR-002), made
/// only inside Infrastructure and only for this one cross-tenant job.
///
/// "Low stock" mirrors InventoryRepository.ListOverviewAsync's definition
/// (QuantityOnHand &lt;= LowStockThreshold) exactly, so a manager's
/// dashboard and their notification feed never disagree about what counts
/// as low — plus an IsActive filter, since alerting on a discontinued
/// product's stock level is noise a report browse doesn't need to avoid.
/// </summary>
public sealed class LowStockNotificationGenerator(GroceryDbContext db) : ILowStockNotificationGenerator
{
    public async Task<int> GenerateAsync(CancellationToken ct)
    {
        var products = db.Products.IgnoreQueryFilters().Where(p => p.IsActive);
        var items = db.InventoryItems.IgnoreQueryFilters();

        var lowStock = await (
            from p in products
            join i in items on p.Id equals i.ProductId into inv
            from item in inv.DefaultIfEmpty()
            let quantityOnHand = item != null ? item.QuantityOnHand : 0
            where quantityOnHand <= p.LowStockThreshold
            select new { p.StoreId, ProductId = p.Id, p.Sku, p.Name, QuantityOnHand = quantityOnHand, p.LowStockThreshold }
        ).ToListAsync(ct);

        if (lowStock.Count == 0)
            return 0;

        // Dedupe: don't raise a second LowStock notification for a product
        // that already has one sitting unread — otherwise every sweep
        // interval would pile up a fresh notification for the same
        // still-unresolved condition.
        var openReferences = await db.Notifications.IgnoreQueryFilters()
            .Where(n => n.Type == NotificationTypes.LowStock && !n.IsRead && n.ReferenceId != null)
            .Select(n => new { n.StoreId, ReferenceId = n.ReferenceId!.Value })
            .ToListAsync(ct);
        var openSet = openReferences.Select(x => (x.StoreId, x.ReferenceId)).ToHashSet();

        var toCreate = lowStock.Where(x => !openSet.Contains((x.StoreId, x.ProductId))).ToList();
        if (toCreate.Count == 0)
            return 0;

        foreach (var x in toCreate)
        {
            var payload = JsonSerializer.Serialize(new
            {
                productId = x.ProductId,
                sku = x.Sku,
                name = x.Name,
                quantityOnHand = x.QuantityOnHand,
                lowStockThreshold = x.LowStockThreshold
            });
            await db.Notifications.AddAsync(new Notification(x.StoreId, NotificationTypes.LowStock, x.ProductId, payload), ct);
        }

        await db.SaveChangesAsync(ct);
        return toCreate.Count;
    }
}
