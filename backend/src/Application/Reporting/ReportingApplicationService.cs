using Application.Common;
using Shared.Exceptions;

namespace Application.Reporting;

public sealed class ReportingApplicationService(IReportingRepository reporting)
{
    public async Task<PagedResult<SalesByDayRow>> GetSalesByDayAsync(DateTime fromUtc, DateTime toUtc, PageRequest page, CancellationToken ct)
    {
        ValidateRange(fromUtc, toUtc);
        var (items, totalCount) = await reporting.GetSalesByDayAsync(fromUtc, toUtc, page.Skip, page.PageSize, ct);
        return new PagedResult<SalesByDayRow> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    public async Task<PagedResult<SalesByProductRow>> GetSalesByProductAsync(DateTime fromUtc, DateTime toUtc, PageRequest page, CancellationToken ct)
    {
        ValidateRange(fromUtc, toUtc);
        var (items, totalCount) = await reporting.GetSalesByProductAsync(fromUtc, toUtc, page.Skip, page.PageSize, ct);
        return new PagedResult<SalesByProductRow> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    public async Task<PagedResult<SalesByCategoryRow>> GetSalesByCategoryAsync(DateTime fromUtc, DateTime toUtc, PageRequest page, CancellationToken ct)
    {
        ValidateRange(fromUtc, toUtc);
        var (items, totalCount) = await reporting.GetSalesByCategoryAsync(fromUtc, toUtc, page.Skip, page.PageSize, ct);
        return new PagedResult<SalesByCategoryRow> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    private static void ValidateRange(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc > toUtc)
            throw new ValidationAppException(new Dictionary<string, string[]> { ["from"] = ["'from' must not be after 'to'."] });
    }
}
