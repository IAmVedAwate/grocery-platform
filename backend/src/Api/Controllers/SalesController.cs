using Api.Authorization;
using Application.Common;
using Application.Sales;
using Domain.Identity;
using Domain.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/sales-orders")]
public sealed class SalesController(SalesApplicationService sales) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ActionResult<PagedResult<SalesOrderDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sales.ListAsync(new PageRequest(page, pageSize), ct);
        return Ok(new PagedResult<SalesOrderDto>
        {
            Items = result.Items.Select(SalesOrderDto.From).ToList(), Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ActionResult<SalesOrderDto>> Get(Guid id, CancellationToken ct) => Ok(SalesOrderDto.From(await sales.GetAsync(id, ct)));

    /// <summary>The checkout endpoint (docs/PRD.md §22). Idempotency-Key is
    /// required — see docs/api/api-conventions.md — so a retried request
    /// (e.g. a dropped response after the sale actually succeeded) returns
    /// the original result instead of creating a duplicate sale.</summary>
    [HttpPost]
    [RequirePermission(Permissions.SalesCreate)]
    public async Task<ActionResult<SalesOrderDto>> Checkout(
        CheckoutDto dto, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { detail = "The Idempotency-Key header is required for checkout." });

        var request = new CheckoutRequest(
            dto.CustomerId, idempotencyKey, dto.PaymentMethod,
            dto.Lines.Select(l => new CheckoutLine(l.ProductId, l.Quantity, l.LineDiscount)).ToList());

        var order = await sales.CheckoutAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, SalesOrderDto.From(order));
    }

    [HttpPost("{id:guid}/refund")]
    [RequirePermission(Permissions.SalesRefund)]
    public async Task<ActionResult<SalesOrderDto>> Refund(Guid id, RefundDto dto, CancellationToken ct)
    {
        var quantities = dto.Lines.ToDictionary(l => l.ProductId, l => l.Quantity);
        var order = await sales.RefundAsync(id, quantities, ct);
        return Ok(SalesOrderDto.From(order));
    }
}

public sealed record CheckoutLineDto(Guid ProductId, int Quantity, decimal? LineDiscount);
public sealed record CheckoutDto(Guid? CustomerId, PaymentMethod PaymentMethod, IReadOnlyList<CheckoutLineDto> Lines);
public sealed record RefundLineDto(Guid ProductId, int Quantity);
public sealed record RefundDto(IReadOnlyList<RefundLineDto> Lines);

public sealed record SalesOrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice, decimal TaxAmount, decimal LineDiscount, int QuantityRefunded)
{
    public static SalesOrderItemDto From(Domain.Sales.SalesOrderItem i) =>
        new(i.ProductId, i.Quantity, i.UnitPrice, i.TaxAmount, i.LineDiscount, i.QuantityRefunded);
}

public sealed record SalesOrderDto(
    Guid Id, Guid? CustomerId, string Status, decimal SubtotalAmount, decimal TaxAmount, decimal DiscountAmount, decimal TotalAmount,
    DateTime CreatedAtUtc, string? InvoiceNumber, IReadOnlyList<SalesOrderItemDto> Items)
{
    public static SalesOrderDto From(Domain.Sales.SalesOrder o) =>
        new(o.Id, o.CustomerId, o.Status.ToString(), o.SubtotalAmount, o.TaxAmount, o.DiscountAmount, o.TotalAmount,
            o.CreatedAtUtc, o.Invoice?.InvoiceNumber, o.Items.Select(SalesOrderItemDto.From).ToList());
}
