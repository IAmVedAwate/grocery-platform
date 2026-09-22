# ADR-010: Agent Orchestration — Microsoft.Extensions.AI vs. Semantic Kernel

**Status:** Accepted — decided and implemented in Phase 5

## Problem

ADR-000's index flagged "Semantic Kernel adoption vs. hand-rolled orchestration" as a decision to make once Phase 5 actually started. The AI assistant (§15 of the PRD) needs to: expose a fixed set of backend-owned tools to the model, let the model decide which tool(s) a question needs, execute the chosen tool(s) with the calling user's real permissions, and feed the result back for a final answer. Something has to own that loop.

## Options Considered

1. **Semantic Kernel** — Microsoft's full agent/orchestration framework: plugins, planners, memory connectors, multi-agent orchestration.
2. **`Microsoft.Extensions.AI` + `Microsoft.Agents.AI`** — the newer, lighter Microsoft abstraction layer: `IChatClient`/`IEmbeddingGenerator` as provider-neutral interfaces, `AIFunctionFactory.Create(...)` to wrap a plain C# method as a callable tool, `IChatClient.AsAIAgent(instructions, tools)` to get a minimal tool-calling agent loop.
3. **Hand-rolled** — call the Gemini SDK's function-calling API directly and write the "did the model ask for a tool → execute it → feed the result back → loop" logic by hand.

## Decision

**`Microsoft.Extensions.AI` + `Microsoft.Agents.AI`.** The assistant's tool registry (`AiTools.cs`) is plain C# methods wrapped with `AIFunctionFactory.Create`; `GeminiAiAssistantService` builds the agent with `chatClient.AsAIAgent(instructions, tools)` and calls `agent.RunAsync(question)`.

## Reasoning

- **Semantic Kernel is scoped for problems this project doesn't have.** There's one assistant, one fixed tool registry per request, no planning across multiple models, no multi-agent handoff, no long-term memory connector. Semantic Kernel's plugin/planner/memory machinery would be infrastructure with no corresponding requirement behind it — the same "don't build for hypothetical future requirements" reasoning applied everywhere else in this codebase.
- **Hand-rolling the tool-calling loop is real, fiddly, provider-specific protocol work** (parsing function-call content out of a response, matching it to a registered tool, serializing the result back into the conversation in the shape the model expects) that `Microsoft.Agents.AI` already provides correctly and is what real teams reach for instead of reinventing it in 2026.
- **`Microsoft.Extensions.AI`'s interfaces are what ADR-009's provider-swap story actually depends on.** `IChatClient`/`IEmbeddingGenerator` are the exact abstraction boundary that made the OpenAI→Gemini pivot a one-file change; adopting Semantic Kernel on top would mean depending on SK's own abstractions instead, layered over these — extra indirection with no offsetting benefit at this scope.
- **It's the direction Microsoft itself is pointing new code**: `Microsoft.Extensions.AI` is the newer, DI-native, framework-integrated abstraction; Semantic Kernel is increasingly positioned as a higher-level orchestration layer that itself can sit on top of it, not a competing foundation.

## Trade-offs

- **Given up:** Semantic Kernel's ecosystem — planners, prompt template engines, and, notably, its memory/vector-store connector story, which duplicates a little of what `CommunityToolkit.VectorData.SqlServer` already provides directly here. If this assistant ever needs multi-step planning across several tools per turn or multi-agent handoff, that's real, uncovered ground.
- **Gained:** a smaller dependency surface, an agent loop that's easy to read end-to-end in one file (`GeminiAiAssistantService.cs`), and an implementation that stays entirely inside the same provider-neutral interfaces the rest of the AI layer (embeddings, vector store) already uses.

## Consequences

- The tool registry lives in `Infrastructure/Ai/AiTools.cs`, deliberately separate from the agent-orchestration code in `Infrastructure/Ai/GeminiAiAssistantService.cs` — not a Semantic-Kernel-specific pattern, just a plain testability seam: `AiTools`' permission-gating and delegation logic is unit-testable with zero LLM/agent framework involved (`tests/Unit/Ai/AiToolsPermissionTests.cs`), while the actual "does the model choose the right tool" behavior is intentionally left to a manual, deliberate, minimal-token smoke test rather than an automated fake (see `docs/testing/testing-strategy.md`) — faking a framework's internal tool-calling protocol well enough to trust the result isn't worth the risk of a subtly-wrong fake giving false confidence.
- If a future requirement genuinely needs multi-agent orchestration or planning, that's a new ADR at that point, not a retrofit of this one.

## Interview Questions This Creates

- "Why not Semantic Kernel, since it's Microsoft's own agent framework?"
- "What's the actual tool-calling loop doing under the hood when the model asks for `get_low_stock_items`?"
- "How would you test that the agent picks the right tool, if you're not faking the LLM?" (Answer: you don't automate that part — it's exactly the one thing worth spending real, deliberate tokens on instead of trusting a fake.)
