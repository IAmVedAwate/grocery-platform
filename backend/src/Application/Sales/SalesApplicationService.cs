using Application.Catalog;
using Application.Common;
using Application.Inventory;
using Domain.Sales;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Sales;

/// <summary>
/// The checkout transaction: validate lines → check + decrement stock →
/// capture payment → issue invoice → audit, all committed in one
/// SaveChangesAsync call (docs/PRD.md §22). If two checkouts race for the
/// last unit of the same product, InventoryItem's RowVersion guarantees
/// exactly one SaveChangesAsync succeeds — the other gets a 409 via
/// GroceryDbContext's DbUpdateConcurrencyException translation, never a
/// negative stock balance (docs/architecture/data-architecture.md).
/// </summary>
public sealed class SalesApplicationService(
    ISalesOrderRepository salesOrders,
    ICustomerRepository customers,
    IProductRepository products,
    InventoryApplicationService inventoryService,
    ITenantContext tenantContext,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork,
    ILogger<SalesApplicationService> logger)
{
    public async Task<SalesOrder> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        // Idempotent retry: a client resubmitting the same Idempotency-Key
        // (e.g. after a dropped response) gets the original result back
        // rather than a duplicate sale (docs/api/api-conventions.md).
        var existing = await salesOrders.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null) return existing;

        if (request.CustomerId is { } customerId)
            _ = await customers.GetByIdAsync(customerId, ct) ?? throw new NotFoundException(nameof(Customer), customerId);

        if (request.Lines.Count == 0)
            throw new ValidationAppException(new Dictionary<string, string[]> { ["lines"] = ["A sale must have at least one line."] });

        var resolvedLines = new List<(Guid ProductId, int Quantity, decimal UnitPrice, decimal TaxAmount, decimal LineDiscount)>();
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ValidationAppException(new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be positive."] });

            var product = await products.GetByIdAsync(line.ProductId, ct)
                ?? throw new NotFoundException(nameof(Domain.Catalog.Product), line.ProductId);
            if (!product.IsActive)
                throw new ConflictAppException($"Product '{product.Name}' is not active and cannot be sold.");

            // Price/tax are computed server-side from the product's current
            // configuration — a client-submitted price is never trusted
            // (docs/business/business-rules-catalog.md).
            var lineDiscount = line.LineDiscount ?? 0m;
            var taxAmount = Math.Round(product.Price * line.Quantity * product.TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
            resolvedLines.Add((product.Id, line.Quantity, product.Price, taxAmount, lineDiscount));
        }

        var order = new SalesOrder(tenantContext.StoreId, request.CustomerId, tenantContext.UserId, request.IdempotencyKey, resolvedLines);

        foreach (var line in resolvedLines)
            await inventoryService.SellAsync(line.ProductId, line.Quantity, nameof(SalesOrder), order.Id, ct);

        DomainRuleGuard.Run(() => order.CapturePayment(request.PaymentMethod));
        DomainRuleGuard.Run(() => order.IssueInvoice(GenerateInvoiceNumber()));

        await salesOrders.AddAsync(order, ct);

        auditWriter.Record("sales.created", nameof(SalesOrder), order.Id.ToString(),
            new { total = order.TotalAmount, lineCount = resolvedLines.Count });

        // A DbUpdateConcurrencyException here (lost the race for stock on
        // any line) surfaces to the caller as ConflictAppException — see
        // GroceryDbContext.SaveChangesAsync.
        await unitOfWork.SaveChangesAsync(ct);

        // A genuine business event, not request plumbing — this is the
        // "log levels used meaningfully" line from docs/ROADMAP.md Phase 3:
        // this is worth an Information line on its own merits, independent
        // of the generic per-request summary UseSerilogRequestLogging()
        // already emits.
        logger.LogInformation(
            "Sale {SalesOrderId} completed for store {StoreId}: {LineCount} lines, total {Total}",
            order.Id, tenantContext.StoreId, resolvedLines.Count, order.TotalAmount);

        return order;
    }

    public async Task<SalesOrder> RefundAsync(Guid salesOrderId, IReadOnlyDictionary<Guid, int> quantityByProductId, CancellationToken ct)
    {
        var order = await salesOrders.GetByIdAsync(salesOrderId, ct) ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);
        DomainRuleGuard.Run(() => order.RefundLines(quantityByProductId));

        foreach (var (productId, quantity) in quantityByProductId)
            await inventoryService.ReceiveAsync(productId, quantity, nameof(SalesOrder), order.Id, ct);

        auditWriter.Record("sales.refunded", nameof(SalesOrder), order.Id.ToString(),
            new { quantityByProductId, resultingStatus = order.Status.ToString() });

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogWarning("Sale {SalesOrderId} refunded for store {StoreId}: status now {Status}",
            order.Id, tenantContext.StoreId, order.Status);
        return order;
    }

    public async Task<SalesOrder> GetAsync(Guid id, CancellationToken ct) =>
        await salesOrders.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(SalesOrder), id);

    public async Task<PagedResult<SalesOrder>> ListAsync(PageRequest page, Guid? customerId, CancellationToken ct)
    {
        var (items, totalCount) = await salesOrders.ListAsync(page.Skip, page.PageSize, customerId, ct);
        return new PagedResult<SalesOrder> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    // A true sequential per-store invoice number needs a dedicated counter
    // (race-prone without one); this is unique and human-readable, which
    // is sufficient for Phase 2 — a real sequence is a documented P2 gap.
    private static string GenerateInvoiceNumber() =>
        $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}

public sealed record CheckoutRequest(Guid? CustomerId, string IdempotencyKey, PaymentMethod PaymentMethod, IReadOnlyList<CheckoutLine> Lines);
public sealed record CheckoutLine(Guid ProductId, int Quantity, decimal? LineDiscount);
