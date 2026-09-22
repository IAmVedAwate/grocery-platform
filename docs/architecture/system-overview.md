# System Overview

## Purpose

QuickStock is a multi-tenant grocery/retail management platform: one deployable backend serving many independently-isolated stores, with a Next.js web client and a future (Phase 6+) Avalonia desktop client sharing the same API. See [PRD.md](../PRD.md) for full requirements and [ROADMAP.md](../ROADMAP.md) for the phased build order.

## High-Level Diagram

```
                    ┌─────────────────────┐
                    │   Next.js Web App    │
                    │  (App Router, TS)     │
                    └──────────┬───────────┘
                               │ HTTPS / JSON (REST, JWT bearer)
                               ▼
                    ┌─────────────────────┐
                    │   ASP.NET Core API    │
                    │   (modular monolith)  │
                    │ ┌─────────────────┐  │
                    │ │ Api             │  │
                    │ ├─────────────────┤  │
                    │ │ Application     │  │  ← use cases / orchestration per module
                    │ ├─────────────────┤  │
                    │ │ Domain          │  │  ← entities, business rules, no framework deps
                    │ ├─────────────────┤  │
                    │ │ Infrastructure  │  │  ← EF Core, external services, storage
                    │ └─────────────────┘  │
                    └──────────┬───────────┘
                               │
          ┌────────────────────┼────────────────────┬─────────────────┐
          ▼                    ▼                     ▼                 ▼
   ┌─────────────┐     ┌──────────────┐      ┌───────────────┐  ┌─────────────┐
   │ SQL Server   │     │ Blob Storage  │      │ Gemini API     │  │ Key Vault    │
   │ (tenant data,│     │ (documents)   │      │ (chat, tools,  │  │ (secrets,    │
   │  vectors)    │     │               │      │  embeddings)   │  │  cloud only) │
   └─────────────┘     └──────────────┘      └───────────────┘  └─────────────┘
```

## Modules

Each business capability is a module within the monolith, with its own `Domain`/`Application` slice and a shared `Infrastructure`/`Shared`:

- **Identity & Tenancy** — users, roles, permissions, tenant resolution, auth.
- **Catalog** — products, categories, brands, units.
- **Inventory** — stock items, stock movements, concurrency-safe stock changes.
- **Purchasing** — suppliers, purchase orders, receiving.
- **Sales** — sales orders, checkout, invoices, payments, refunds.
- **Customers** — customer profiles and purchase history.
- **Reporting** — read-optimized query endpoints over the above.
- **Notifications** — background-generated alerts.
- **Documents** — file metadata + storage abstraction, RAG source material.
- **AI** — tool registry, RAG pipeline, structured output handling.
- **Audit** — append-only cross-cutting audit log, written to by every module.

See [backend-architecture.md](./backend-architecture.md) for the dependency-direction rules governing how these modules relate.

## Key Cross-Cutting Concerns

- **Multi-tenancy** — every module's data is implicitly scoped to a `Store`. See [data-architecture.md](./data-architecture.md) and [ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md).
- **Authentication/Authorization** — see [authentication-flow.md](./authentication-flow.md).
- **AI/RAG** — see [ai-architecture.md](./ai-architecture.md).
- **Deployment** — see [deployment-architecture.md](./deployment-architecture.md).

## What This System Deliberately Is Not

Per [PRD.md §3](../PRD.md#3-non-goals): not a microservices system, not (yet) running on Kubernetes, not a cross-tenant analytics product, not shipping a desktop client in the core phases. These are scoped-out by design, not by oversight — see the relevant ADRs and the roadmap's Phase 6+ section for why and when they might change.
