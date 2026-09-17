using Application.Identity;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class StoreRepository(GroceryDbContext db) : IStoreRepository
{
    public async Task AddAsync(Store store, CancellationToken ct) => await db.Stores.AddAsync(store, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        db.Stores.AnyAsync(s => s.Slug == slug.ToLowerInvariant(), ct);

    public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct) =>
        db.Stores.FirstOrDefaultAsync(s => s.Slug == slug.ToLowerInvariant(), ct);
}
