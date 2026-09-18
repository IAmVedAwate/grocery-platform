using Application.Catalog;
using Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class CategoryRepository(GroceryDbContext db) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken ct) => await db.Categories.AddAsync(category, ct);

    public async Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
}
