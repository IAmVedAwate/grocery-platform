using Application.Purchasing;
using Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class PurchaseOrderRepository(GroceryDbContext db) : IPurchaseOrderRepository
{
    public async Task AddAsync(PurchaseOrder order, CancellationToken ct) => await db.PurchaseOrders.AddAsync(order, ct);

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListAsync(int skip, int take, PurchaseOrderStatus? status, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc).Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }
}
