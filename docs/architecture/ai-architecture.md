# AI Architecture

See [PRD §15–16](../PRD.md#15-ai-requirements), [ADR-003](../decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md) (vector store), [ADR-009](../decisions/ADR-009-llm-provider-gemini-direct.md) (provider) and [ADR-010](../decisions/ADR-010-agent-orchestration-microsoft-extensions-ai.md) (orchestration framework). This document describes the **as-built** Phase 5 implementation — it replaced an earlier, OpenAI-based plan; see the ADRs above for what changed and why.

## One Agent, Two Kinds of Tools

The original plan treated tool-calling and RAG as two separate capabilities. The actual implementation unifies them: `search_documents` is registered as just another tool, on equal footing with the business-data tools. The model decides, per question, whether it needs live business data, uploaded-document content, both, or neither — there's no hardcoded router that guesses intent before the model sees the question.

```
User asks a question (e.g. "which products are below reorder level?"
                        or "what's our return policy?")
        │
        ▼
AssistantController.Ask requires the ai.assistant.use permission
(RequirePermissionAttribute — an authorization POLICY, evaluated before
the controller, IChatClient, or Gemini are ever touched)
        │
        ▼
GeminiAiAssistantService builds an agent (chatClient.AsAIAgent) with the
FIXED tool registry below, and calls agent.RunAsync(question)
        │
        ▼
Microsoft.Agents.AI drives the loop: the model responds with either a
direct answer or a request to call one or more tools; each tool call is
executed and its result fed back, until the model produces a final answer
        │
        ▼
Response: free-text answer + which tools were used + any document
citations (DocumentId, FileName, Snippet) collected along the way
```

**Tool registry** (`Infrastructure/Ai/AiTools.cs`): `search_products`, `get_inventory`, `get_low_stock_items`, `get_sales_summary`, `get_purchase_order_status`, `get_supplier_status`, `get_customer_summary`, `search_documents`. Every tool wraps an existing `Application` use case (or, for `search_documents`, the vector store directly) — there is no AI-only code path that reads data differently than the REST API does.

**Why the tool logic lives in a separate class from the agent loop:** `AiTools` has zero dependency on `IChatClient`/`Microsoft.Agents.AI` — it's plain async C# methods. `GeminiAiAssistantService` only wires those methods up as `AIFunction`s via `AIFunctionFactory.Create` and drives `RunAsync`. This split is what makes the permission-gating and delegation logic unit-testable (`tests/Unit/Ai/AiToolsPermissionTests.cs`) without any LLM involved — see [ADR-010](../decisions/ADR-010-agent-orchestration-microsoft-extensions-ai.md).

**Guardrails:**
- Tool allow-list is fixed in backend code (`AiTools`'s public methods); the model cannot invent a tool name that executes anything else.
- **Every tool independently re-checks the calling user's real permission claim** (`AiTools.EnsurePermission`, reading the same `permission` claims the REST API's own `[RequirePermission]` checks) before touching any collaborator — a user without `sales.create` cannot get the assistant to look up customer data on their behalf, because the tool itself refuses, exactly like the equivalent controller action would. This is checked at two layers: the controller requires `ai.assistant.use` just to reach the agent at all, and each tool additionally requires its own specific permission.
- No tool accepts or constructs raw SQL, ever — every tool calls into an existing `Application` service.
- `search_documents` filters the vector search itself by the caller's `StoreId` (`VectorSearchOptions<DocumentChunk>.Filter`) — see "Tenant Isolation" below.

## RAG: Document Upload, Storage, and Retrieval

```
Document upload (.txt/.md only — see "Scope" below)
        │
        ▼
DocumentApplicationService validates content-type/size, reads the text,
saves the raw file (IStorageService), persists a Document row (EF Core,
relational), then calls IDocumentIngestionService AFTER the row commits
        │
        ▼
TextChunker splits the text: paragraph-aware, greedily packed to 1000
chars, any single oversized paragraph hard-split on its own — pure,
dependency-free logic (tests/Unit/Ai/TextChunkerTests.cs)
        │
        ▼
Each chunk becomes a DocumentChunk record (StoreId, DocumentId, FileName,
ChunkIndex, Content, Vector) upserted into a CommunityToolkit.VectorData
VectorStore collection backed by a SQL Server 2025 native `vector` column
— the SAME database as every relational table, not a second datastore
(ADR-003). Embedding happens automatically on upsert because the
collection is configured with an EmbeddingGenerator.
        │
        ▼
User asks a question → the agent decides to call search_documents →
collection.SearchAsync(query, top: 5, Filter: c => c.StoreId == callerStoreId)
— the tenant filter is a REAL pre-filter on the vector query itself, not
a post-filter applied to already-retrieved results
        │
        ▼
Matching chunks are returned to the model as the tool's result (prefixed
with "[Source: filename]"); each one is also recorded as a DocumentCitation
(DocumentId, FileName, a short snippet) surfaced back to the caller
        │
        ▼
The model answers using that content; the system prompt explicitly
instructs it to say "not found" rather than answer from general
knowledge if search_documents finds nothing relevant
```

**Why tenant-scoping happens *in* the vector query, not after:** filtering retrieved results after the fact would still mean the similarity search itself ranked across another tenant's documents. Passing `StoreId` as a `VectorSearchOptions.Filter` removes that possibility structurally — proven by `tests/Integration/SearchDocumentsTenantIsolationTests.cs`, which uploads a document as one store and asserts a second store's `search_documents` call never surfaces it, consistent with [ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md)'s "defense at the query, not just the display layer" principle.

**Scope:** only `.txt`/`.md` documents (max 2MB) are supported. PDF/DOCX text extraction is real, separate work (layout parsing, and OCR for scanned pages) that was deliberately left out rather than half-built — the chunk → embed → retrieve → cite pipeline is fully proven either way.

**Deliberate simplification — no chunk overlap:** unlike many RAG references, chunks don't overlap (see `TextChunker.cs`). This is a documented first-pass simplification, not a hidden gap: a fact split exactly across a paragraph boundary could be harder to retrieve. Overlap is a reasonable next iteration, not required to demonstrate the architecture.

## Testing Philosophy — "Minimum Tokens"

Every other test suite in this project hits real infrastructure (real SQL Server via Testcontainers, real ONNX model) on purpose. The AI layer is the deliberate exception, for a concrete reason: the real Gemini API costs real tokens on every call, and a CI-triggered test run happens far more often than a developer would ever manually re-verify an LLM integration.

- **`FakeEmbeddingGenerator`** (`tests/Integration/Fakes/FakeEmbeddingGenerator.cs`) replaces the real Gemini-backed `IEmbeddingGenerator<string,Embedding<float>>` in `CustomWebApplicationFactory` — deterministic (same input text always produces the same vector), so the *real* chunking → SQL Server vector upsert → tenant-filtered retrieval pipeline is exercised for real, without a single real embeddings call.
- **`IChatClient` (the agent/tool-calling loop) is deliberately NOT faked.** Faking it well enough to drive `Microsoft.Agents.AI`'s real tool-calling protocol convincingly would risk a subtly-wrong fake giving false confidence about behavior that's only meaningful with a real model anyway ("did it pick the right tool for this question"). Instead:
  - `AiTools`' permission-gating and delegation logic is covered by pure unit tests (`AiToolsPermissionTests.cs`) that need no LLM at all.
  - `AssistantController`'s permission gate is covered by a real HTTP integration test (`AssistantPermissionTests.cs`) that proves a 403 never even constructs `IChatClient` (authorization runs before the controller is activated).
  - The "does the agent actually answer correctly" path is **one deliberate, manual, minimal-token live smoke test**, run once a real `GEMINI_API_KEY` is available locally — never part of the automated suite.
- Every AI-related DI registration in `Program.cs` is a lazy factory delegate, resolved only on first real use — the whole application boots and every non-AI feature works with no `GEMINI_API_KEY` configured at all.
- **`CustomWebApplicationFactory` blanks `GEMINI_API_KEY` for the entire suite.** This is the guarantee that makes "we don't fake the chat client" safe: `WebApplicationFactory` boots the real `Program` and therefore inherits the developer's own user-secrets, so once a real key exists locally (as it must, to run the smoke test), *any* test reaching an AI path would otherwise bill a live call on every run. Blanking it means such a test fails loudly with "GEMINI_API_KEY is not configured" instead of quietly charging. This was a real incident, not a hypothetical — see `docs/operations/troubleshooting.md`.

## Known Gaps (honestly scoped out of Phase 5)

- **No `AiInteractionLog`/interaction-level observability table.** Requests/responses aren't currently persisted for later analysis beyond the standard structured request logging (Serilog + correlation IDs) already in place for every endpoint. A dedicated interaction log (prompt, tool calls, latency, outcome) is a reasonable Phase 6 addition, not built here.
- **No explicit adversarial prompt-injection test.** The system prompt instructs the model to treat tool/document content as data, not instructions, but no adversarial document (e.g., one containing "ignore previous instructions...") has been tested against it yet.
- **No explicit tool-call-iteration cap configured.** `Microsoft.Agents.AI`'s agent loop is used with its defaults; an explicit max-iterations guard against a runaway loop hasn't been added.

## Interview Questions This Creates

- "Walk me through the full function-calling loop, concretely, for one example."
- "How do you stop the AI from doing something the user isn't authorized to do?"
- "How is tenant isolation enforced in the RAG retrieval step specifically, and how do you know it actually works?"
- "Why does `search_documents` live in the same tool registry as the business-data tools instead of a separate RAG pipeline?"
- "Your test suite hits real infrastructure everywhere else — why is the AI layer different, and what's the one place you draw the line?"
- "What's missing from this AI layer that you'd want before calling it production-ready?"
