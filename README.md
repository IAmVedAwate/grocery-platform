# QuickStock — Enterprise Grocery Management Platform

A multi-tenant SaaS platform for grocery and small retail stores: fast product registration, fast billing, accurate real-time inventory, and (once enough stores are onboarded) AI-assisted business insight grounded in real data — built to replace the Excel-plus-expensive-legacy-software pattern most independent stores are stuck with today.

This repository is also a structured engineering demonstration: every significant architecture decision is documented with its reasoning, alternatives, and trade-offs, and every capability is built to be defensible in a technical interview, not just to run a demo.

## Status

Planning complete. Implementation in progress per [docs/ROADMAP.md](./docs/ROADMAP.md), currently Phase 1 (Foundation, Identity & Multi-Tenant Core).

## Start Here

- **[docs/PRD.md](./docs/PRD.md)** — the full product requirements document.
- **[docs/ROADMAP.md](./docs/ROADMAP.md)** — the 5-phase implementation plan with engineering/learning/interview/evidence checkpoints per phase.
- **[docs/decisions/](./docs/decisions/)** — Architecture Decision Records for every major technical choice.
- **[docs/architecture/](./docs/architecture/)** — system, backend, data, auth, AI, and deployment architecture in detail.

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| Backend | ASP.NET Core (.NET 10), EF Core | Modular monolith — see [ADR-001](./docs/decisions/ADR-001-modular-monolith-vs-microservices.md); .NET 10 over .NET 8 — see [ADR-008](./docs/decisions/ADR-008-target-framework-net10-vs-net8.md) |
| Database | SQL Server (2025), including native `VECTOR` type for RAG | See [ADR-003](./docs/decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md) |
| Web client | Next.js (App Router) + TypeScript, Tailwind + shadcn/ui | Minimal, professional, architecture-first |
| Auth | ASP.NET Core Identity + JWT + rotating refresh tokens, permission-based authorization | See [ADR-004](./docs/decisions/ADR-004-authentication-architecture.md) |
| AI | OpenAI API — function calling, structured outputs, RAG | See [ADR-005](./docs/decisions/ADR-005-openai-direct-vs-azure-openai.md) |
| Cloud | Azure (SQL, Blob Storage, Key Vault, App Service/Container Apps, App Insights) | Phase 4 |
| CI/CD | GitHub Actions | Build → test → containerize → deploy |
| Testing | xUnit, FluentAssertions, Testcontainers (real SQL Server, not mocks) | See [docs/testing/testing-strategy.md](./docs/testing/testing-strategy.md) |

## Repository Structure

```
grocery-platform/
├── backend/       ASP.NET Core API (Api/Application/Domain/Infrastructure/Shared)
├── web/           Next.js client
├── ai/            Tool registry, prompt, and RAG pipeline notes
├── database/      SQL scripts, seed data
├── deployment/    Docker, docker-compose, Azure/K8s manifests
├── docs/          PRD, roadmap, ADRs, architecture, security, testing, operations
└── scripts/       Developer/ops utility scripts
```

## Local Development

See [docs/operations/local-development.md](./docs/operations/local-development.md).

## Multi-Tenancy & Security

Every store's data is strictly isolated at the query layer and proven so by dedicated integration tests — see [docs/decisions/ADR-002-multi-tenancy-strategy.md](./docs/decisions/ADR-002-multi-tenancy-strategy.md) and [docs/security/security-model.md](./docs/security/security-model.md).

## What This Project Is Not (Yet)

Deliberately out of the 5-phase core scope: microservices, a live Kubernetes deployment, an Avalonia desktop client, cross-tenant aggregate analytics, and self-service billing. See [docs/PRD.md §3 Non-Goals](./docs/PRD.md#3-non-goals) and the Phase 6+ section of [docs/ROADMAP.md](./docs/ROADMAP.md) for why and when these might change.

## License

Portfolio project — not currently licensed for reuse.
