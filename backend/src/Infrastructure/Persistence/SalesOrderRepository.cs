using Application.Sales;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class SalesOrderRepository(GroceryDbContext db) : ISalesOrderRepository
{
    public async Task AddAsync(SalesOrder order, CancellationToken ct) => await db.SalesOrders.AddAsync(order, ct);

    public Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Query().FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<SalesOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        Query().FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);

    public async Task<(IReadOnlyList<SalesOrder> Items, int TotalCount)> ListAsync(int skip, int take, Guid? customerId, CancellationToken ct)
    {
        var query = Query(asNoTracking: true);
        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc).Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }

    private IQueryable<SalesOrder> Query(bool asNoTracking = false)
    {
        var query = db.SalesOrders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.Invoice)
            .AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }
}
