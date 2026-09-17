using Api.Authorization;
using Application.Common;
using Application.Purchasing;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/suppliers")]
public sealed class SuppliersController(SupplierApplicationService suppliers) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<PagedResult<SupplierDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? activeOnly = null, CancellationToken ct = default)
    {
        var result = await suppliers.ListAsync(new PageRequest(page, pageSize), activeOnly, ct);
        return Ok(new PagedResult<SupplierDto>
        {
            Items = result.Items.Select(SupplierDto.From).ToList(), Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<SupplierDto>> Get(Guid id, CancellationToken ct)
    {
        var supplier = await suppliers.GetAsync(id, ct);
        return Ok(SupplierDto.From(supplier));
    }

    [HttpPost]
    [RequirePermission(Permissions.PurchaseCreate)]
    public async Task<ActionResult<SupplierDto>> Create(CreateSupplierDto dto, CancellationToken ct)
    {
        var supplier = await suppliers.CreateAsync(dto.Name, dto.ContactInfo, dto.PaymentTermsDays, ct);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, SupplierDto.From(supplier));
    }
}

public sealed record CreateSupplierDto(string Name, string? ContactInfo, int? PaymentTermsDays);
public sealed record SupplierDto(Guid Id, string Name, string? ContactInfo, string Status, int? PaymentTermsDays)
{
    public static SupplierDto From(Domain.Purchasing.Supplier s) => new(s.Id, s.Name, s.ContactInfo, s.Status.ToString(), s.PaymentTermsDays);
}
