using Api.Authorization;
using Application.Common;
using Application.Purchasing;
using Domain.Identity;
using Domain.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/purchase-orders")]
public sealed class PurchaseOrdersController(PurchasingApplicationService purchasing) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] PurchaseOrderStatus? status = null, CancellationToken ct = default)
    {
        var result = await purchasing.ListAsync(new PageRequest(page, pageSize), status, ct);
        return Ok(new PagedResult<PurchaseOrderDto>
        {
            Items = result.Items.Select(PurchaseOrderDto.From).ToList(), Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<PurchaseOrderDto>> Get(Guid id, CancellationToken ct)
    {
        var order = await purchasing.GetAsync(id, ct);
        return Ok(PurchaseOrderDto.From(order));
    }

    [HttpPost]
    [RequirePermission(Permissions.PurchaseCreate)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        var order = await purchasing.CreateAsync(
            dto.SupplierId, dto.Lines.Select(l => new CreatePurchaseOrderLine(l.ProductId, l.Quantity, l.UnitCost)).ToList(), ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, PurchaseOrderDto.From(order));
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(Permissions.PurchaseCreate)]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(Guid id, CancellationToken ct) =>
        Ok(PurchaseOrderDto.From(await purchasing.SubmitAsync(id, ct)));

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permissions.PurchaseApprove)]
    public async Task<ActionResult<PurchaseOrderDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(PurchaseOrderDto.From(await purchasing.ApproveAsync(id, ct)));

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.PurchaseCreate)]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(PurchaseOrderDto.From(await purchasing.CancelAsync(id, ct)));

    [HttpPost("{id:guid}/receive")]
    [RequirePermission(Permissions.InventoryAdjust)]
    public async Task<ActionResult<PurchaseOrderDto>> Receive(Guid id, ReceivePurchaseOrderDto dto, CancellationToken ct)
    {
        var quantities = dto.Lines.ToDictionary(l => l.ProductId, l => l.Quantity);
        var order = await purchasing.ReceiveAsync(id, quantities, ct);
        return Ok(PurchaseOrderDto.From(order));
    }
}

public sealed record CreatePurchaseOrderLineDto(Guid ProductId, int Quantity, decimal UnitCost);
public sealed record CreatePurchaseOrderDto(Guid SupplierId, IReadOnlyList<CreatePurchaseOrderLineDto> Lines);
public sealed record ReceiveLineDto(Guid ProductId, int Quantity);
public sealed record ReceivePurchaseOrderDto(IReadOnlyList<ReceiveLineDto> Lines);

public sealed record PurchaseOrderItemDto(Guid ProductId, int QuantityOrdered, decimal UnitCost, int QuantityReceived)
{
    public static PurchaseOrderItemDto From(Domain.Purchasing.PurchaseOrderItem i) => new(i.ProductId, i.QuantityOrdered, i.UnitCost, i.QuantityReceived);
}

public sealed record PurchaseOrderDto(Guid Id, Guid SupplierId, string Status, Guid? ApprovedByUserId, DateTime CreatedAtUtc, IReadOnlyList<PurchaseOrderItemDto> Items)
{
    public static PurchaseOrderDto From(Domain.Purchasing.PurchaseOrder o) =>
        new(o.Id, o.SupplierId, o.Status.ToString(), o.ApprovedByUserId, o.CreatedAtUtc, o.Items.Select(PurchaseOrderItemDto.From).ToList());
}
