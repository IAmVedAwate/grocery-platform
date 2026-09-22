using Api.Authorization;
using Application.Ai;
using Application.Common;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
public sealed class DocumentsController(DocumentApplicationService documents) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.AiAssistantUse)]
    public async Task<ActionResult<PagedResult<DocumentDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await documents.ListAsync(new PageRequest(page, pageSize), ct);
        return Ok(new PagedResult<DocumentDto>
        {
            Items = result.Items.Select(DocumentDto.From).ToList(), Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount
        });
    }

    [HttpPost]
    [RequirePermission(Permissions.AiAssistantUse)]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var document = await documents.UploadAsync(stream, file.FileName, file.ContentType, file.Length, ct);
        return Created($"/api/v1/documents/{document.Id}", DocumentDto.From(document));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.AiAssistantUse)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}

public sealed record DocumentDto(Guid Id, string FileName, string ContentType, DateTime UploadedAtUtc)
{
    public static DocumentDto From(Domain.Ai.Document d) => new(d.Id, d.FileName, d.ContentType, d.UploadedAtUtc);
}
