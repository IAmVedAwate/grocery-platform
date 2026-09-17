using Domain;

namespace Application.Identity;

public interface IStoreRepository
{
    Task AddAsync(Store store, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task<Store?> GetBySlugAsync(string slug, CancellationToken ct);
}
