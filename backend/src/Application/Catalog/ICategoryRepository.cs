using Domain.Catalog;

namespace Application.Catalog;

/// <summary>
/// Implemented in Infrastructure against GroceryDbContext. Reads rely on
/// the DbContext's Category query filter for tenant scoping, same as
/// IProductRepository.
/// </summary>
public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken ct);
    Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken ct);
}
