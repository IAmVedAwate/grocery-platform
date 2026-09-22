# ADR Index

Architecture Decision Records for the Enterprise Grocery Management Platform. Each ADR follows: Problem → Options Considered → Decision → Reasoning → Trade-offs → Consequences.

| # | Title | Status | Phase |
|---|---|---|---|
| [001](./ADR-001-modular-monolith-vs-microservices.md) | Modular monolith vs. microservices | Accepted | 1 |
| [002](./ADR-002-multi-tenancy-strategy.md) | Multi-tenancy strategy | Accepted | 1 |
| [003](./ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md) | Vector store for RAG | Accepted | 5 (decided early) |
| [004](./ADR-004-authentication-architecture.md) | Authentication & session architecture | Accepted | 1 |
| [005](./ADR-005-openai-direct-vs-azure-openai.md) | LLM provider (original plan) | **Superseded by 009** | 5 (decided early) |
| [006](./ADR-006-background-processing-approach.md) | Background processing approach | Accepted | 3 |
| [007](./ADR-007-caching-strategy.md) | Caching strategy | Accepted | 3 |
| [008](./ADR-008-target-framework-net10-vs-net8.md) | Target framework: .NET 10 vs. .NET 8 | Accepted | 1 |
| [009](./ADR-009-llm-provider-gemini-direct.md) | LLM provider — Gemini direct (supersedes 005) | Accepted | 5 |
| [010](./ADR-010-agent-orchestration-microsoft-extensions-ai.md) | Agent orchestration: Microsoft.Extensions.AI vs. Semantic Kernel | Accepted | 5 |

Future ADRs to be added during implementation (not yet written, listed here so scope isn't lost):
- Azure compute target: App Service vs. Container Apps (Phase 4)
- Offset vs. keyset pagination for the sales/audit activity feed (Phase 2/3, once real access patterns exist)
