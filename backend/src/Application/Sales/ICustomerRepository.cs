using Domain.Sales;

namespace Application.Sales;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(int skip, int take, string? search, CancellationToken ct);
}
