using Domain.Purchasing;

namespace Application.Purchasing;

public interface IPurchaseOrderRepository
{
    Task AddAsync(PurchaseOrder order, CancellationToken ct);
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> ListAsync(int skip, int take, PurchaseOrderStatus? status, CancellationToken ct);
}
