using Api.Authorization;
using Application.Ai;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/assistant")]
public sealed class AssistantController(IAiAssistantService assistant) : ControllerBase
{
    [HttpPost("ask")]
    [RequirePermission(Permissions.AiAssistantUse)]
    public async Task<ActionResult<AssistantAnswerDto>> Ask(AskDto dto, CancellationToken ct)
    {
        var answer = await assistant.AskAsync(dto.Question, ct);
        return Ok(AssistantAnswerDto.From(answer));
    }
}

public sealed record AskDto(string Question);

public sealed record CitationDto(Guid DocumentId, string FileName, string Snippet)
{
    public static CitationDto From(DocumentCitation c) => new(c.DocumentId, c.FileName, c.Snippet);
}

public sealed record AssistantAnswerDto(string Text, IReadOnlyList<string> ToolsUsed, IReadOnlyList<CitationDto> Citations)
{
    public static AssistantAnswerDto From(AssistantAnswer a) =>
        new(a.Text, a.ToolsUsed, a.Citations.Select(CitationDto.From).ToList());
}
