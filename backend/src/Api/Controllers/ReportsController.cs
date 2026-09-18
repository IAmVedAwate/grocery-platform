using Api.Authorization;
using Application.Common;
using Application.Reporting;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(ReportingApplicationService reporting) : ControllerBase
{
    [HttpGet("sales-by-day")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ActionResult<PagedResult<SalesByDayRow>>> SalesByDay(
        [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 31, CancellationToken ct = default) =>
        Ok(await reporting.GetSalesByDayAsync(from, to, new PageRequest(page, pageSize), ct));

    [HttpGet("sales-by-product")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ActionResult<PagedResult<SalesByProductRow>>> SalesByProduct(
        [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await reporting.GetSalesByProductAsync(from, to, new PageRequest(page, pageSize), ct));

    [HttpGet("sales-by-category")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<ActionResult<PagedResult<SalesByCategoryRow>>> SalesByCategory(
        [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await reporting.GetSalesByCategoryAsync(from, to, new PageRequest(page, pageSize), ct));

    // Low-stock reporting is already served by GET /api/v1/inventory?lowStockOnly=true
    // (docs/PRD.md §47) — not duplicated here.
}
