using Application.Common;
using Domain.Sales;
using Shared.Exceptions;

namespace Application.Sales;

public sealed class CustomerApplicationService(ICustomerRepository customers, ITenantContext tenantContext, IUnitOfWork unitOfWork)
{
    public async Task<Customer> CreateAsync(string name, string? phone, string? email, CancellationToken ct)
    {
        var customer = new Customer(tenantContext.StoreId, name, phone, email);
        await customers.AddAsync(customer, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer> GetAsync(Guid id, CancellationToken ct) =>
        await customers.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Customer), id);

    public async Task<PagedResult<Customer>> ListAsync(PageRequest page, string? search, CancellationToken ct)
    {
        var (items, totalCount) = await customers.ListAsync(page.Skip, page.PageSize, search, ct);
        return new PagedResult<Customer> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }
}
