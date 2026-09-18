using Api.Authorization;
using Application.Catalog;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
public sealed class CategoriesController(CategoryApplicationService categories) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.CatalogRead)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken ct)
    {
        var items = await categories.ListActiveAsync(ct);
        return Ok(items.Select(CategoryDto.From).ToList());
    }

    [HttpPost]
    [RequirePermission(Permissions.CatalogManage)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto, CancellationToken ct)
    {
        var category = await categories.CreateAsync(dto.Name, ct);
        return Created($"/api/v1/categories/{category.Id}", CategoryDto.From(category));
    }
}

public sealed record CreateCategoryDto(string Name);

public sealed record CategoryDto(Guid Id, string Name)
{
    public static CategoryDto From(Domain.Catalog.Category c) => new(c.Id, c.Name);
}
