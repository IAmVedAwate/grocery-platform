using Domain.Sales;

namespace Application.Sales;

public interface ISalesOrderRepository
{
    Task AddAsync(SalesOrder order, CancellationToken ct);
    Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<SalesOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<(IReadOnlyList<SalesOrder> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct);
}
