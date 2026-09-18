using Application.Common;
using Domain.Catalog;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Catalog;

/// <summary>
/// Categories are read far more often than written (every product list/edit
/// screen loads the full active set for a dropdown; a store adds a handful
/// of categories a year). That read/write ratio is what makes this a good
/// IMemoryCache candidate (docs/ROADMAP.md Phase 3) — Product's own list
/// endpoint is NOT cached here, because it's paginated/filtered per request
/// and has no such small, stable, whole-set shape to cache.
///
/// IMemoryCache is a single in-process instance shared by every request
/// regardless of tenant, so the cache key MUST include StoreId — caching
/// under a bare "categories" key would leak store A's categories to store
/// B's next request. This is the same class of mistake as the EF Core
/// global-query-filter bug (docs/operations/troubleshooting.md), just at
/// the caching layer instead of the query layer.
/// </summary>
public sealed class CategoryApplicationService(
    ICategoryRepository categories,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<Category> CreateAsync(string name, CancellationToken ct)
    {
        var category = new Category(tenantContext.StoreId, name);
        await categories.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Invalidate rather than wait out the TTL — a store owner who just
        // added a category expects to see it in the next dropdown load, not
        // up to 5 minutes later.
        cache.Remove(CacheKey(tenantContext.StoreId));

        return category;
    }

    public async Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken ct)
    {
        var key = CacheKey(tenantContext.StoreId);
        if (cache.TryGetValue(key, out IReadOnlyList<Category>? cached) && cached is not null)
            return cached;

        var items = await categories.ListActiveAsync(ct);
        cache.Set(key, items, CacheDuration);
        return items;
    }

    private static string CacheKey(Guid storeId) => $"categories:active:{storeId}";
}
