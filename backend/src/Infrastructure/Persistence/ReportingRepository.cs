using Application.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Every query here follows the same discipline learned the hard way in
/// InventoryRepository (docs/operations/troubleshooting.md): aggregate
/// and order on raw column expressions, and only project into the final
/// DTO as the very last step, after Skip/Take — never order on an
/// already-materialized custom record.
/// </summary>
public sealed class ReportingRepository(GroceryDbContext db) : IReportingRepository
{
    public async Task<(IReadOnlyList<SalesByDayRow> Items, int TotalCount)> GetSalesByDayAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct)
    {
        var grouped = db.SalesOrders.AsNoTracking()
            .Where(o => o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc <= toUtc)
            .GroupBy(o => o.CreatedAtUtc.Date);

        var totalCount = await grouped.CountAsync(ct);

        var raw = await grouped
            .OrderByDescending(g => g.Key)
            .Skip(skip)
            .Take(take)
            .Select(g => new { Date = g.Key, OrderCount = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);

        // DateOnly conversion and the average-order-value division happen
        // in memory, on the already-paged page — not translatable SQL
        // Server expressions, and don't need to be.
        var items = raw
            .Select(x => new SalesByDayRow(
                DateOnly.FromDateTime(x.Date), x.OrderCount, x.Revenue,
                x.OrderCount == 0 ? 0m : Math.Round(x.Revenue / x.OrderCount, 2)))
            .ToList();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<SalesByProductRow> Items, int TotalCount)> GetSalesByProductAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct)
    {
        // NOTE: no "into g select g" here — that trailing select wraps the
        // GroupBy in an untranslatable Select(g => g) that breaks CountAsync
        // and further composition ("Translation of 'Select' which contains
        // grouping parameter without composition is not supported").
        var grouped =
            from item in db.SalesOrderItems.AsNoTracking()
            join order in db.SalesOrders.AsNoTracking() on item.SalesOrderId equals order.Id
            join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
            where order.CreatedAtUtc >= fromUtc && order.CreatedAtUtc <= toUtc
            group item by new { item.ProductId, product.Sku, product.Name };

        var totalCount = await grouped.CountAsync(ct);

        var items = await grouped
            .OrderByDescending(g => g.Sum(i => i.Quantity * i.UnitPrice + i.TaxAmount - i.LineDiscount))
            .Skip(skip)
            .Take(take)
            .Select(g => new SalesByProductRow(
                g.Key.ProductId, g.Key.Sku, g.Key.Name,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * i.UnitPrice + i.TaxAmount - i.LineDiscount)))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<SalesByCategoryRow> Items, int TotalCount)> GetSalesByCategoryAsync(
        DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct)
    {
        // LEFT JOIN to Category — a product with no category assigned still
        // contributes to the report under "Uncategorized" rather than being
        // silently dropped.
        var grouped =
            from item in db.SalesOrderItems.AsNoTracking()
            join order in db.SalesOrders.AsNoTracking() on item.SalesOrderId equals order.Id
            join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
            join cat in db.Categories.AsNoTracking() on product.CategoryId equals cat.Id into catJoin
            from cat in catJoin.DefaultIfEmpty()
            where order.CreatedAtUtc >= fromUtc && order.CreatedAtUtc <= toUtc
            group item by new { product.CategoryId, CategoryName = cat != null ? cat.Name : "Uncategorized" };

        var totalCount = await grouped.CountAsync(ct);

        var items = await grouped
            .OrderByDescending(g => g.Sum(i => i.Quantity * i.UnitPrice + i.TaxAmount - i.LineDiscount))
            .Skip(skip)
            .Take(take)
            .Select(g => new SalesByCategoryRow(
                g.Key.CategoryId, g.Key.CategoryName,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * i.UnitPrice + i.TaxAmount - i.LineDiscount)))
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
