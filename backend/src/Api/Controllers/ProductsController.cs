using Api.Authorization;
using Application.Catalog;
using Application.Common;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/products")]
public sealed class ProductsController(ProductApplicationService products) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<PagedResult<ProductDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await products.ListAsync(new PageRequest(page, pageSize), search, isActive, ct);
        return Ok(new PagedResult<ProductDto>
        {
            Items = result.Items.Select(ProductDto.From).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(id, ct);
        return Ok(ProductDto.From(product));
    }

    [HttpPost]
    [RequirePermission(Permissions.CatalogManage)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto, CancellationToken ct)
    {
        var product = await products.CreateAsync(
            new CreateProductRequest(dto.Sku, dto.Name, dto.Price, dto.TaxRatePercent, dto.Barcode,
                dto.CategoryId, dto.BrandId, dto.UnitId, dto.LowStockThreshold), ct);

        return CreatedAtAction(nameof(Get), new { id = product.Id }, ProductDto.From(product));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CatalogManage)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductDto dto, CancellationToken ct)
    {
        var product = await products.UpdateAsync(id,
            new UpdateProductRequest(dto.Name, dto.Price, dto.TaxRatePercent, dto.Barcode,
                dto.CategoryId, dto.BrandId, dto.UnitId, dto.LowStockThreshold), ct);

        return Ok(ProductDto.From(product));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CatalogManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await products.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/image")]
    [RequirePermission(Permissions.CatalogManage)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SetImage(Guid id, IFormFile image, CancellationToken ct)
    {
        await using var stream = image.OpenReadStream();
        await products.SetImageAsync(id, stream, image.ContentType, image.Length, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/image")]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken ct)
    {
        var (content, contentType) = await products.GetImageAsync(id, ct);
        return File(content, contentType);
    }

    [HttpDelete("{id:guid}/image")]
    [RequirePermission(Permissions.CatalogManage)]
    public async Task<IActionResult> RemoveImage(Guid id, CancellationToken ct)
    {
        await products.RemoveImageAsync(id, ct);
        return NoContent();
    }
}

public sealed record CreateProductDto(string Sku, string Name, decimal Price, decimal TaxRatePercent, string? Barcode, Guid? CategoryId, Guid? BrandId, Guid? UnitId, int LowStockThreshold);
public sealed record UpdateProductDto(string Name, decimal Price, decimal TaxRatePercent, string? Barcode, Guid? CategoryId, Guid? BrandId, Guid? UnitId, int LowStockThreshold);

public sealed record ProductDto(Guid Id, string Sku, string? Barcode, string Name, decimal Price, decimal TaxRatePercent, bool IsActive, int LowStockThreshold, bool HasImage, string? DominantColorHex)
{
    public static ProductDto From(Domain.Catalog.Product p) =>
        new(p.Id, p.Sku, p.Barcode, p.Name, p.Price, p.TaxRatePercent, p.IsActive, p.LowStockThreshold, p.ImageStorageKey is not null, p.DominantColorHex);
}
