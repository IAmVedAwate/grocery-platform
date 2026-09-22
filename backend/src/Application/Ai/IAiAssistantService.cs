namespace Application.Ai;

/// <summary>
/// The assistant as a whole — tool-calling over live business data
/// (docs/PRD.md §15) plus RAG over uploaded documents (§16), combined
/// into one agent that decides per question which (if any) tools to call.
/// Implemented in Infrastructure (Microsoft.Agents.AI/Gemini specifics
/// never leak into Application) — see GeminiAiAssistantService.
/// </summary>
public interface IAiAssistantService
{
    Task<AssistantAnswer> AskAsync(string question, CancellationToken ct);
}

public sealed record AssistantAnswer(string Text, IReadOnlyList<string> ToolsUsed, IReadOnlyList<DocumentCitation> Citations);

public sealed record DocumentCitation(Guid DocumentId, string FileName, string Snippet);
