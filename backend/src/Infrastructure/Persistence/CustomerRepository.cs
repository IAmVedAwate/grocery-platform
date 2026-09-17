using Application.Sales;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class CustomerRepository(GroceryDbContext db) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken ct) => await db.Customers.AddAsync(customer, ct);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(int skip, int take, string? search, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)));

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }
}
