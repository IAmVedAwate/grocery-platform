# ADR-009: LLM Provider — Gemini Direct (supersedes ADR-005)

**Status:** Accepted — decided and implemented in Phase 5

## Problem

[ADR-005](./ADR-005-openai-direct-vs-azure-openai.md) planned OpenAI direct as the LLM provider for the AI assistant (tool-calling, §15) and RAG (§16). By the time Phase 5 actually started, that plan met a real constraint: no OpenAI billing account was set up, while a Google Gemini API key was already provisioned and had been proven out end-to-end in a separate, smaller reference project (a RAG pipeline over personal notes) using the same target architecture — SQL Server native vector storage, `Microsoft.Extensions.AI` abstractions, tool-calling agents. Re-doing that setup/validation work against OpenAI instead, for no functional gain, wasn't worth blocking Phase 5's start on.

## Options Considered

1. **Stay with OpenAI direct** (ADR-005's original plan) — set up billing, use the OpenAI .NET SDK.
2. **Switch to Google Gemini direct** — reuse an already-provisioned, already-tested API key and a proven reference implementation of the exact same architecture (vector storage, tool-calling agent, embeddings).
3. **Azure OpenAI** — already rejected once in ADR-005 for the same reasons (provisioning/quota risk); nothing about this pivot changes that reasoning.

## Decision

**Google Gemini, called directly** via the official `Google.GenAI` .NET SDK, adapted to the same `Microsoft.Extensions.AI` abstractions (`IChatClient`, `IEmbeddingGenerator<TInput,TEmbedding>`) ADR-005 already committed to for provider-agnosticism. The provider changed; the abstraction boundary ADR-005 established did not — this is exactly the swap that boundary was designed to absorb.

## Reasoning

- **A working key beats a hypothetical one.** OpenAI billing wasn't set up; Gemini's was, and had already been exercised against the identical architectural shape (SQL Server native vector + tool-calling) in a separate reference project, which meant Phase 5 could start immediately instead of waiting on account setup.
- **The abstraction ADR-005 specified made this a config change, not a rewrite.** Because the AI layer was always meant to sit behind `Microsoft.Extensions.AI`'s provider-neutral interfaces rather than a vendor SDK's own types, swapping the concrete provider touches exactly one file (`Program.cs`'s DI registration for `Client`/`IChatClient`/`IEmbeddingGenerator`) — nothing in `Application.Ai` or the tool registry (`AiTools.cs`) references Gemini, OpenAI, or Azure OpenAI at all.
- **The learning objectives are unchanged.** Function/tool calling, structured retrieval, RAG grounding with citations, and tenant-scoped authorization are the things actually being demonstrated (§15–16 of the PRD) — which model vendor serves the completions is an infrastructure detail, exactly as ADR-005 already argued.
- **Cost control for automated testing.** Gemini's `gemini-3.5-flash-lite` (chat) and `gemini-embedding-2-preview` (embeddings) were chosen as the default models specifically for low per-call cost, since the intent from the start was to spend real API tokens only once, deliberately, via a manual smoke test — never in the automated suite (see "Consequences" below and `docs/testing/testing-strategy.md`).

## Trade-offs

- **Given up:** exact parity with the most commonly-referenced provider in .NET/Azure job postings (OpenAI/Azure OpenAI) — an interviewer unfamiliar with Gemini's .NET SDK is a real, if minor, possibility.
- **Gained:** Phase 5 actually shipped instead of stalling on billing setup, a working reference implementation to build from instead of a blank page, and a stronger interview answer than either original option alone — "I designed this behind a provider-neutral interface, and here's the real pivot that proves the abstraction actually holds" is a better story than a plan that was never tested against a change.

## Consequences

- **`GEMINI_API_KEY`/`GEMINI_CHAT_MODEL`/`GEMINI_EMBEDDING_MODEL`/`GEMINI_EMBEDDING_DIMENSIONS`** replace the OpenAI equivalents in `.env.example`, following the exact same "never in source control" discipline as every other credential in the system (§17 of the PRD).
- **Every AI-related DI registration in `Program.cs` is a lazy factory delegate** (`sp => ...`), resolved only on first actual use — the whole application boots and every non-AI feature works correctly with `GEMINI_API_KEY` unset. Only `/api/v1/assistant/ask` and document upload (which needs an embedding) require it.
- **A real regression, found by hand, fixed, and now covered by a test:** registering the Gemini-backed `IEmbeddingGenerator` directly was still one layer too eager — `CommunityToolkit.VectorData.SqlServer`'s `VectorStore` resolves `IEmbeddingGenerator` the moment `VectorStore` itself is constructed, and `VectorStore` is a constructor dependency of every AI-adjacent class, including `DocumentsController`'s `List`/`Delete` actions, which never actually embed anything. The result: `GET /api/v1/documents` threw a 500 with no `GEMINI_API_KEY` configured, directly contradicting the claim above. Fixed with `Infrastructure/Ai/LazyGeminiEmbeddingGenerator.cs`, a thin wrapper that defers resolving the real generator until `GenerateAsync` is actually called, not merely when something holds a reference to it — same discipline, one layer deeper. Caught manually (curl against the running dev server, not the test suite — the suite's `FakeEmbeddingGenerator` masked it completely, since it's substituted unconditionally); now guarded by `tests/Integration/DocumentsWorkWithoutGeminiKeyTests.cs`, which reverts just that one override back to the real `LazyGeminiEmbeddingGenerator` inside a `WithWebHostBuilder`-derived factory to prove the production registration path itself — not the test fake — tolerates a missing key.
- **The automated test suite never calls the real Gemini API.** `CustomWebApplicationFactory` substitutes a deterministic `FakeEmbeddingGenerator` for `IEmbeddingGenerator<string,Embedding<float>>`, which is enough to exercise the real chunking → SQL Server native vector upsert → tenant-scoped retrieval pipeline end-to-end without spending tokens. The chat/tool-calling agent loop (`IChatClient`) is deliberately **not** faked — see [ADR-010](./ADR-010-agent-orchestration-microsoft-extensions-ai.md) and `AiTools.cs` for why that specific piece is covered by one manual, minimal-token live smoke test instead of an automated fake.

## Interview Questions This Creates

- "ADR-005 says OpenAI — why does the code use Gemini?" (Answer: this ADR, and it's a genuinely good answer — a documented pivot beats a plan nobody ever executed.)
- "What would actually change in your code to swap providers again?" (One DI registration block in `Program.cs`; nothing in `Application` or the tool registry.)
- "Why didn't you just edit ADR-005 to say Gemini instead of writing a new one?" (ADRs record decisions *as of the time they were made* — rewriting history erases the reasoning that led to the pivot, which is itself useful information.)
