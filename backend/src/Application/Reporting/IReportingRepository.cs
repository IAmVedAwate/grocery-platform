namespace Application.Reporting;

/// <summary>
/// Pure read-side projections — no Domain entities, no business
/// invariants to enforce, just aggregation over already-validated data.
/// Every query is implicitly tenant-scoped the same way everything else
/// is (docs/decisions/ADR-002): SalesOrder/SalesOrderItem/Product already
/// carry EF Core's global query filter, so a report can't leak another
/// store's numbers just by existing.
/// </summary>
public interface IReportingRepository
{
    Task<(IReadOnlyList<SalesByDayRow> Items, int TotalCount)> GetSalesByDayAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct);

    Task<(IReadOnlyList<SalesByProductRow> Items, int TotalCount)> GetSalesByProductAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct);

    Task<(IReadOnlyList<SalesByCategoryRow> Items, int TotalCount)> GetSalesByCategoryAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct);
}

// Revenue here is gross (line total at time of sale) — a refund-adjusted
// "net revenue" report is a reasonable follow-up, not built in this phase
// (it needs a decision on whether a refund attributes back to the
// original sale date or the refund date, which is a business-rules
// question, not a query-writing one).
public sealed record SalesByDayRow(DateOnly Date, int OrderCount, decimal Revenue, decimal AverageOrderValue);
public sealed record SalesByProductRow(Guid ProductId, string Sku, string Name, int QuantitySold, decimal Revenue);
public sealed record SalesByCategoryRow(Guid? CategoryId, string CategoryName, int QuantitySold, decimal Revenue);
