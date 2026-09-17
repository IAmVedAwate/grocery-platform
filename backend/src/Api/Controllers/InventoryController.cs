using Api.Authorization;
using Application.Common;
using Application.Inventory;
using Domain.Identity;
using Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/inventory")]
public sealed class InventoryController(InventoryApplicationService inventory) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.InventoryRead)]
    public async Task<ActionResult<PagedResult<InventoryOverviewRow>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool lowStockOnly = false,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await inventory.ListOverviewAsync(new PageRequest(page, pageSize), search, lowStockOnly, ct);
        return Ok(new PagedResult<InventoryOverviewRow>
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount
        });
    }

    [HttpGet("{productId:guid}/history")]
    [RequirePermission(Permissions.InventoryRead)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> History(Guid productId, CancellationToken ct)
    {
        var movements = await inventory.GetHistoryAsync(productId, ct);
        return Ok(movements.Select(StockMovementDto.From).ToList());
    }

    [HttpPost("{productId:guid}/adjust")]
    [RequirePermission(Permissions.InventoryAdjust)]
    public async Task<ActionResult<InventoryItemDto>> Adjust(Guid productId, AdjustInventoryDto dto, CancellationToken ct)
    {
        var item = await inventory.AdjustAsync(productId, dto.QuantityDelta, dto.Reason, ct);
        return Ok(InventoryItemDto.From(item));
    }
}

public sealed record AdjustInventoryDto(int QuantityDelta, string Reason);
public sealed record InventoryItemDto(Guid ProductId, int QuantityOnHand)
{
    public static InventoryItemDto From(Domain.Inventory.InventoryItem i) => new(i.ProductId, i.QuantityOnHand);
}

public sealed record StockMovementDto(Guid Id, string Type, int QuantityDelta, string? ReferenceType, Guid? ReferenceId, string? Reason, int ResultingQuantity, DateTime CreatedAtUtc)
{
    public static StockMovementDto From(Domain.Inventory.StockMovement m) =>
        new(m.Id, m.Type.ToString(), m.QuantityDelta, m.ReferenceType, m.ReferenceId, m.Reason, m.ResultingQuantity, m.CreatedAtUtc);
}
