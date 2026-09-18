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

## Phase 5 — AI, RAG & Interview Polish (P1/P2)

**Scope**
- OpenAI function-calling tool registry (`get_inventory`, `search_products`, `get_sales_summary`, `get_low_stock_items`, `get_purchase_order_status`, `get_supplier_status`, `get_customer_summary`), tenant- and permission-scoped execution.
- Structured output schemas for tool arguments and final answers.
- RAG pipeline: document upload → chunk → embed → SQL Server native `VECTOR` storage → tenant-scoped similarity retrieval → grounded answer with citations.
- AI interaction logging (request, tool calls, latency, retrieval sources) for basic evaluation.
- AI guardrails implemented and tested: tool allow-list, max tool-call loop count, prompt-injection-aware system framing.
- Final documentation pass: all ADRs finalized, README completed, demo scenarios scripted, resume/skill mapping written, interview-question review across all five phases.

**Vertical slice built:** a manager asks the AI assistant a real inventory question (answered via tool call against live data) and a real policy question (answered via RAG with a citation), both respecting the asker's actual permissions.

**Checkpoints**
- *Engineering:* the assistant never answers a data question from model memory when a tool exists for it, and never answers a document question without a retrieved, cited source.
- *Learning:* the full function-calling loop (model → tool selection → backend validation/execution → result → model → response); chunking/embedding/retrieval trade-offs; why authorization must be re-checked at tool-execution time, not trusted from the prompt.
- *Interview:* "How do you stop an LLM from doing something a user isn't allowed to do?" "RAG vs fine-tuning — why RAG here?" "How do you evaluate whether retrieval quality is good?"
- *Evidence:* recorded demo scenarios (from the PRD's scenario list) showing both tool-calling and RAG paths, with an adversarial prompt-injection test case in the AI test suite.

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
