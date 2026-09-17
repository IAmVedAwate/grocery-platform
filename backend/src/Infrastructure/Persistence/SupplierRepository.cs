using Application.Purchasing;
using Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class SupplierRepository(GroceryDbContext db) : ISupplierRepository
{
    public async Task AddAsync(Supplier supplier, CancellationToken ct) => await db.Suppliers.AddAsync(supplier, ct);

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> ListAsync(int skip, int take, bool? activeOnly, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (activeOnly == true)
            query = query.Where(s => s.Status == SupplierStatus.Active);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.Name).Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }
}
