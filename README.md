# QuickStock — Multi-Tenant Grocery Management Platform

A multi-tenant SaaS platform for grocery and small retail stores: fast product registration, barcode-first billing, accurate real-time inventory, purchasing, and an AI assistant that answers questions about a store's own live data and uploaded documents.

Built to replace the Excel-plus-expensive-legacy-software pattern most independent stores are stuck with — and built so that **every significant architecture decision is written down with its alternatives and trade-offs**, not just made. If you only read one thing beyond this page, read [docs/decisions/](./docs/decisions/).

**Stack:** ASP.NET Core (.NET 10) · EF Core · SQL Server 2025 · Next.js 16 / React 19 · TypeScript · Tailwind v4 · Google Gemini · Docker · GitHub Actions

---

## Status

| Phase | Scope | State |
|---|---|---|
| 1 | Foundation, identity, multi-tenancy, catalog | ✅ Done |
| 2 | Inventory, purchasing, sales/checkout, customers | ✅ Done |
| 3 | Security hardening, performance, observability | ✅ Done |
| 4 | Cloud deployment (Azure) | ⏳ Not started |
| 5 | AI tool-calling + RAG | ✅ Done |

**133 tests passing** — 3 architecture (NetArchTest), 63 unit (xUnit), 67 integration (WebApplicationFactory + Testcontainers against a real SQL Server, not mocks).

Known gaps are named explicitly rather than left implied — see [docs/checkpoints/skills-inventory.md](./docs/checkpoints/skills-inventory.md#4-real-gaps-named-honestly).

---

## Run it locally

Needs Docker Desktop, the .NET 10 SDK, and Node LTS.

```bash
git clone <this repo> && cd grocery-platform
cp .env.example .env                 # fill in local values — never commit .env

# SQL Server
docker compose -f deployment/docker-compose.yml up -d

# API — http://localhost:5292 (migrations apply automatically in Development)
cd backend/src/Api && dotnet run

# Web — http://localhost:3000
cd web && npm install && npm run dev
```

Then register a store at `/register` and you're in. Full walkthrough: [docs/operations/local-development.md](./docs/operations/local-development.md).

**No Gemini API key is needed** to run everything except the AI assistant and document upload — the AI client is resolved lazily, so the app boots and every other feature works without one. The test suite never calls the real API at all.

```bash
cd backend && dotnet test   # all three suites
```

---

## What's actually interesting here

**Multi-tenancy that's proven, not asserted.** Shared schema with EF Core global query filters keyed on a tenant context resolved *only* from the validated JWT — never a header or body field. A dedicated integration suite attempts cross-tenant reads and asserts they fail, returning 404 rather than 403 (a 403 would confirm the record exists). Real multi-tenancy bugs were caught this way during development — a stale tenant context leaking into a global query filter, and Identity's defaults assuming a single global tenant — both written up in [troubleshooting.md](./docs/operations/troubleshooting.md). → [ADR-002](./docs/decisions/ADR-002-multi-tenancy-strategy.md)

**Per-user permissions, not roles.** Access is a per-person checklist of permission keys, editable at any time. Roles exist only to seed the first admin's set at registration. A cashier and a manager differ by exactly the keys ticked for them.

**Concurrency-safe checkout.** The "two cashiers, last two units" race is handled with rowversion concurrency tokens and covered by a test that fires simultaneous sales and asserts exactly one wins.

**AI with the authorization boundary in the right place.** One agent exposes eight tools; the model picks which to call, but every tool independently re-checks the *caller's* real permission claims before executing, and RAG retrieval applies the tenant filter as a pre-filter inside the vector query rather than filtering results afterwards. A document from one store can never surface in another's answer — [proven by its own test](./backend/tests/Integration/SearchDocumentsTenantIsolationTests.cs). → [ai-architecture.md](./docs/architecture/ai-architecture.md)

**RAG without new infrastructure.** Embeddings live in SQL Server 2025's native `vector` column in the same database as everything else — no second datastore. → [ADR-003](./docs/decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md)

**A documented provider pivot.** The plan was OpenAI ([ADR-005](./docs/decisions/ADR-005-openai-direct-vs-azure-openai.md)); the implementation is Gemini ([ADR-009](./docs/decisions/ADR-009-llm-provider-gemini-direct.md), which supersedes it). Because the AI layer was built against `Microsoft.Extensions.AI`'s provider-neutral interfaces, the swap touched one DI registration block. The old ADR is kept rather than rewritten — the reasoning that led to the change is itself useful.

**Tests that hit real infrastructure.** Real SQL Server via Testcontainers, a real ONNX model for product colour extraction, real file I/O. The one deliberate exception is the LLM: a fake embedding generator keeps the suite token-free, and the credential is explicitly blanked in the test host so a configured key can never turn a CI run into a bill. → [testing-strategy.md](./docs/testing/testing-strategy.md)

---

## Repository layout

```
backend/
  src/{Api,Application,Domain,Infrastructure,Shared}   dependency direction enforced by tests
  tests/{Unit,Integration,Architecture}
web/          Next.js app — token-based design system, ⌘K command palette, drawer UI
docs/
  PRD.md · ROADMAP.md
  decisions/    10 ADRs — the reasoning behind every major choice
  architecture/ system, backend, data, auth, AI, deployment
  security/ testing/ operations/ business/ checkpoints/
deployment/   Dockerfiles, docker-compose
```

`Domain` has zero outward dependencies — no EF Core, no ASP.NET Core, no AI SDK — and an architecture test fails the build if that ever stops being true.

---

## License

Portfolio project — not currently licensed for reuse.
