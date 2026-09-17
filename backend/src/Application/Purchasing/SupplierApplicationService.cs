using Application.Common;
using Domain.Purchasing;
using Shared.Exceptions;

namespace Application.Purchasing;

public sealed class SupplierApplicationService(ISupplierRepository suppliers, ITenantContext tenantContext, IUnitOfWork unitOfWork)
{
    public async Task<Supplier> CreateAsync(string name, string? contactInfo, int? paymentTermsDays, CancellationToken ct)
    {
        var supplier = new Supplier(tenantContext.StoreId, name, contactInfo, paymentTermsDays);
        await suppliers.AddAsync(supplier, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return supplier;
    }

    public async Task<Supplier> GetAsync(Guid id, CancellationToken ct) =>
        await suppliers.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Supplier), id);

    public async Task<PagedResult<Supplier>> ListAsync(PageRequest page, bool? activeOnly, CancellationToken ct)
    {
        var (items, totalCount) = await suppliers.ListAsync(page.Skip, page.PageSize, activeOnly, ct);
        return new PagedResult<Supplier> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }
}
