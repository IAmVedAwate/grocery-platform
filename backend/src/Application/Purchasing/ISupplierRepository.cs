using Domain.Purchasing;

namespace Application.Purchasing;

public interface ISupplierRepository
{
    Task AddAsync(Supplier supplier, CancellationToken ct);
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> ListAsync(int skip, int take, bool? activeOnly, CancellationToken ct);
}
