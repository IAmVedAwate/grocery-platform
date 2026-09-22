# Implementation Roadmap

Five core phases, timeboxed to roughly **4 focused hours/day**, sequenced by dependency and built as **vertical slices** — each business capability ships backend + API + database + web UI + tests together, rather than building all layers for all modules separately. This keeps the system demoable after every phase instead of only at the very end.

Each phase ends with four checkpoints, per the original planning brief:
- **Engineering checkpoint** — what capability was actually built.
- **Learning checkpoint** — what the developer must understand, not just have generated.
- **Interview checkpoint** — questions the developer should now be able to answer unaided.
- **Evidence checkpoint** — what's visible in the repo/tests/logs/demo to prove it.

---

## Phase 1 — Foundation, Identity & Multi-Tenant Core (P0)

**Scope**
- Solution scaffolding: `Api / Application / Domain / Infrastructure / Shared` with enforced dependency direction.
- Docker Compose for local dev: API container + SQL Server container.
- GitHub Actions CI skeleton: restore → build → unit tests.
- Tenant (`Store`) model; tenant resolution middleware from JWT claims.
- ASP.NET Core Identity + custom JWT issuance + rotating refresh tokens (reuse detection).
- Permission-based authorization framework (policy-per-permission, not role-string checks).
- Audit logging plumbing (append-only `AuditLog`, write-side helper used by later modules).
- Catalog module: Product, Category, Brand, Unit — full CRUD, tenant-scoped, paginated/filtered.
- Next.js scaffold: auth pages (login/register), protected layout, product list/create pages.
- Unit tests (validation/business rules) and integration tests (auth flow, tenant isolation) from day one.

**Vertical slice built:** register a store → log in → manage a product catalog.

**Checkpoints**
- *Engineering:* a tenant can be created, a user can authenticate, and a permission-gated endpoint correctly rejects an unauthorized but authenticated user.
- *Learning:* JWT structure and validation, refresh token rotation and reuse detection, EF Core global query filters, dependency-direction enforcement in a modular monolith.
- *Interview:* "Explain access token vs refresh token." "How do you prevent a stolen refresh token from being reused silently?" "How does your system stop tenant A from reading tenant B's data — what's the actual enforcement point?"
- *Evidence:* integration test that logs in as Tenant A and asserts a Tenant B product ID returns 404; architecture test asserting `Domain` has no reference to `Infrastructure`.

Full write-up, with file/line references and interview answers in first person: [checkpoints/phase-1-checkpoint-explanation.md](./checkpoints/phase-1-checkpoint-explanation.md).

---

## Phase 2 — Core Business Workflows (P0)

**Scope**
- Inventory: `InventoryItem` + `StockMovement`, concurrency-safe stock decrement (rowversion + transaction).
- Suppliers + Purchase Orders: draft → submit → approve → receive (partial receipt supported).
- Sales/Checkout: cart → stock validation → payment capture → invoice → inventory update → audit, atomic.
- Customers: optional association on sales, basic profile + history.
- Next.js: inventory screens, purchase order workflow, checkout screen (barcode/keyboard-first UX), sales history.
- Integration tests covering the full checkout transaction and the concurrency scenario explicitly.

**Vertical slice built:** create a supplier → raise and receive a purchase order → sell the received stock through checkout → see inventory and sales history reflect it.

**Checkpoints**
- *Engineering:* a full purchase-to-sale cycle works end-to-end through the UI; a deliberately concurrent test (two simultaneous "last unit" sale requests) proves exactly one succeeds.
- *Learning:* optimistic concurrency vs pessimistic locking and why rowversion was chosen here; what makes a multi-step operation "transactional" in EF Core; SCOPE_IDENTITY/multi-row insert pitfalls (from the developer's own prior SQL mistakes) and how this design avoids them.
- *Interview:* "How do you prevent overselling inventory under concurrent requests?" "What happens if payment succeeds but invoice creation fails?" "Why rowversion instead of a database lock here?"
- *Evidence:* the concurrency integration test in the repo with its assertions; a demo recording/screenshot of the full purchase→receive→sell flow.

Full write-up, with file/line references and interview answers in first person: [checkpoints/phase-2-checkpoint-explanation.md](./checkpoints/phase-2-checkpoint-explanation.md).

---

## Phase 3 — Security Hardening, Performance & Observability (P1)

**Scope**
- Rate limiting on auth endpoints; CORS finalized per environment.
- Centralized exception handling → Problem Details responses.
- Serilog structured logging with correlation ID middleware.
- `/health/live` and `/health/ready` endpoints.
- Reporting/dashboard endpoints (sales by day/product/category, low-stock report) — paginated, indexed.
- SQL performance pass: identify at least one genuinely slow query against a realistically sized seeded dataset, fix it, measure before/after with execution plans and `STATISTICS IO/TIME`.
- `IMemoryCache` for catalog/category reads with explicit TTL and invalidation on write.
- Background `IHostedService` worker for low-stock notification generation.

**Vertical slice built:** a manager opens the reports dashboard and sees fast, correct, paginated numbers; a low-stock event generates a notification without any manual trigger.

**Checkpoints**
- *Engineering:* a report endpoint measurably uses an index seek; a request failure can be traced end-to-end by correlation ID in the logs.
- *Learning:* SARGability, covering indexes, scan-vs-seek, why `Information`-level-everything logging is not observability.
- *Interview:* "Walk me through diagnosing a slow endpoint." "What's a covering index and when does it matter here?" "Why is this cached and what invalidates it?"
- *Evidence:* a documented before/after performance note (query, execution plan summary, logical reads, elapsed time) committed to the repo.

---

## Phase 4 — Cloud Deployment & Azure (P1)

**Scope**
- Azure SQL, Azure Blob Storage (documents), Azure Key Vault (secrets), compute target decided (App Service vs Container Apps — recorded as an ADR once compared).
- GitHub Actions deploy stage: build → push image → deploy to Azure on merge to main.
- Application Insights wired for request/dependency/exception telemetry.
- Environment separation finalized: local/dev/staging/production configuration, no secret ever in source control.
- Kubernetes manifest set (`Deployment`, `Service`, `ConfigMap`, `Secret`, readiness/liveness probes) produced and documented as an artifact — not deployed to a live cluster.

**Vertical slice built:** the same system running locally is now reachable at a public demo URL, backed by managed cloud services, with secrets pulled from Key Vault instead of local config.

**Checkpoints**
- *Engineering:* a fresh clone + documented steps reproduces the deployment; a secret rotated in Key Vault takes effect without a code change or redeploy.
- *Learning:* what Key Vault + managed identity actually does at the token level; image vs container; what a K8s readiness probe protects against even without running a cluster.
- *Interview:* "Why Azure SQL over self-managed SQL Server on a VM?" "Where do your secrets live and how does the app get them?" "What would you change to deploy this to Kubernetes for real?"
- *Evidence:* live demo URL; `docs/operations/deployment.md` reproducible by a stranger; Application Insights dashboard showing real request telemetry.

---

## Phase 5 — AI, RAG & Interview Polish (P1/P2) — Built

**Scope (as built — provider pivoted from the original plan; see [ADR-009](./decisions/ADR-009-llm-provider-gemini-direct.md))**
- Gemini-backed function-calling tool registry (`search_products`, `get_inventory`, `get_low_stock_items`, `get_sales_summary`, `get_purchase_order_status`, `get_supplier_status`, `get_customer_summary`, `search_documents`), tenant- and permission-scoped execution, orchestrated via `Microsoft.Extensions.AI`/`Microsoft.Agents.AI` rather than Semantic Kernel ([ADR-010](./decisions/ADR-010-agent-orchestration-microsoft-extensions-ai.md)).
- Tool-call arguments are schema-validated automatically (`AIFunctionFactory`); the final answer is free text with a separate citations list — **not** itself a JSON-schema-validated structured output (a scoped-down version of the original plan).
- RAG pipeline: document upload (.txt/.md) → paragraph-aware chunking → embed → SQL Server native `vector` storage (`CommunityToolkit.VectorData.SqlServer`) → tenant-scoped similarity retrieval (a real pre-filter, not post-filtering) → grounded answer with citations — `search_documents` is a tool in the same registry above, not a separate pipeline.
- AI guardrails implemented: fixed tool allow-list, per-tool permission re-check against the caller's real claims, tenant-scoped vector retrieval. **Not** implemented: an explicit max-tool-call-loop cap, and prompt-injection-aware framing is in the system prompt but untested against an adversarial document — both named honestly in `docs/checkpoints/skills-inventory.md` rather than left silently unstated.
- **Not built:** AI interaction logging (request/tool-calls/latency/retrieval sources) for evaluation — standard request logging (Serilog + correlation IDs) covers it generically, nothing AI-specific.
- Testing: the automated suite never spends real Gemini tokens (`FakeEmbeddingGenerator` covers real chunk/vector storage/retrieval; the tool-calling agent loop is covered by unit tests on the pure permission/delegation logic plus a real-HTTP 403 test, not by faking the LLM) — one manual, deliberate, minimal-token live smoke test against the real Gemini API is the only place real tokens are spent. See [ai-architecture.md](./architecture/ai-architecture.md#testing-philosophy--minimum-tokens).

**Vertical slice built:** a manager asks the AI assistant a real inventory question (answered via tool call against live data) and a real policy question (answered via RAG with a citation), both respecting the asker's actual permissions — proven end-to-end by `tests/Integration/SearchDocumentsTenantIsolationTests.cs`, `AssistantPermissionTests.cs`, and `DocumentsCrudTests.cs`, with the actual "does the agent answer correctly" path reserved for the manual smoke test.

**Checkpoints**
- *Engineering:* the assistant never answers a data question from model memory when a tool exists for it (every tool independently re-checks the caller's permission before executing — proven by `AiToolsPermissionTests.cs`); a document from one store never surfaces in another store's `search_documents` results (proven by `SearchDocumentsTenantIsolationTests.cs`, the RAG equivalent of Phase 1's `TenantIsolationTests.cs`).
- *Learning:* the full function-calling loop (model → tool selection → backend validation/execution → result → model → response); chunking/embedding/retrieval trade-offs; why authorization must be re-checked at tool-execution time, not trusted from the prompt; what changes (and what doesn't) when you pivot LLM providers mid-project behind a real abstraction.
- *Interview:* "How do you stop an LLM from doing something a user isn't allowed to do?" "RAG vs fine-tuning — why RAG here?" "Why Gemini when your ADR originally said OpenAI?" "Why didn't you fake the LLM in your test suite the way you mock everything else?"
- *Evidence:* `docs/decisions/ADR-009*`/`ADR-010*` (the pivot, documented not hidden), `docs/architecture/ai-architecture.md` (as-built design + named gaps), the four new integration test files, `AiToolsPermissionTests.cs`/`TextChunkerTests.cs` (unit).

---

## Phase 6+ — Explicit Stretch (outside core scope)

Not scheduled, not required for the portfolio milestone. Only pursued after Phases 1–5 are stable and demonstrable:

- Avalonia desktop billing/counter client (reusing the same backend APIs).
- Live Kubernetes deployment of the manifests produced in Phase 4.
- Redis — only if a concrete, measured need emerges (not by default).
- MCP exposure of the existing tool registry to external AI clients.
- Multi-agent orchestration (only if a real workflow benefits from role separation).
- Cross-tenant aggregate analytics / data product (requires an anonymization design and a real multi-tenant dataset).
- Self-service tenant sign-up and subscription billing.
