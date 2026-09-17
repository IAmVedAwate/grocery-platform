# ADR-005: LLM Provider — OpenAI Direct vs. Azure OpenAI

**Status:** Accepted — decided in Phase 1 planning, implemented in Phase 5

## Problem

The AI assistant (§15–16 of the PRD) needs an LLM provider for function/tool calling, structured outputs, and embeddings. The reference target-skill resume favors Azure OpenAI, which also ties into the project's broader Azure learning goal (Key Vault, Azure Monitor).

## Options Considered

1. **Azure OpenAI Service** — same underlying models, provisioned through Azure, integrates with Key Vault/Managed Identity/Azure Monitor from the start.
2. **OpenAI API directly** — simplest possible setup, fastest to iterate on during development.

## Decision

**OpenAI API directly for Phases 1–5.** Azure OpenAI is documented here as the intended production migration path but is **not built in the core scope** — the integration is designed behind an interface so the provider can be swapped without touching business logic, and the migration itself becomes a future ADR once actually performed.

## Reasoning

- Azure OpenAI requires resource provisioning and, in some regions/subscriptions, quota approval — a dependency outside the developer's control that risks blocking Phase 5 on an external approval queue.
- The AI/RAG learning objectives (function calling, structured outputs, embeddings, RAG grounding, guardrails) are identical regardless of which endpoint serves the model — the provider is an infrastructure detail behind an `ILlmClient`-style abstraction, not the substance of what's being demonstrated.
- Building against OpenAI directly first, then documenting (rather than building) the Azure OpenAI migration, avoids doing the same integration work twice inside an already-tight timeline, while still letting the developer speak credibly to the migration path in an interview ("I built it provider-agnostic and can point to exactly what changes for Azure OpenAI").
- This keeps Phase 5's Azure-cost surface area limited to what Phase 4 already established (App Service/Container Apps, SQL, Blob, Key Vault), rather than adding a new Azure service under time pressure at the very end of the project.

## Trade-offs

- **Given up:** the tighter Azure-native integration story (Managed Identity token flow straight to the model endpoint) and the exact match to the reference resume's stated stack.
- **Gained:** faster Phase 5 start, no quota/approval risk, and a provider-agnostic AI layer that's arguably a *better* engineering answer than hardcoding to one vendor.

## Consequences

- The AI integration layer must be written against an internal abstraction (not the OpenAI SDK's types leaking into `Application`/`Domain`), so the eventual Azure OpenAI swap is a configuration and adapter change, not a rewrite.
- API keys (OpenAI) still follow the same secrets discipline as every other credential in the system (§17 of the PRD) — never in source control, Key Vault-managed in the cloud deployment.

## Interview Questions This Creates

- "Why OpenAI direct instead of Azure OpenAI, given the rest of your stack is Azure?"
- "What would actually change in your code to move to Azure OpenAI?"
- "How is your AI integration layer isolated from the specific provider SDK?"
