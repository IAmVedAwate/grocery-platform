using Api.Authorization;
using Application.Common;
using Application.Sales;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/customers")]
public sealed class CustomersController(CustomerApplicationService customers) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.SalesCreate)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await customers.ListAsync(new PageRequest(page, pageSize), search, ct);
        return Ok(new PagedResult<CustomerDto>
        {
            Items = result.Items.Select(CustomerDto.From).ToList(), Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.SalesCreate)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken ct) => Ok(CustomerDto.From(await customers.GetAsync(id, ct)));

    [HttpPost]
    [RequirePermission(Permissions.SalesCreate)]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto dto, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(dto.Name, dto.Phone, dto.Email, ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, CustomerDto.From(customer));
    }
}

public sealed record CreateCustomerDto(string Name, string? Phone, string? Email);
public sealed record CustomerDto(Guid Id, string Name, string? Phone, string? Email)
{
    public static CustomerDto From(Domain.Sales.Customer c) => new(c.Id, c.Name, c.Phone, c.Email);
}
