# Product Requirements Document
## Enterprise Grocery Management Platform (working name: "QuickStock")

| | |
|---|---|
| Status | Approved for Phase 1 |
| Owner | Solo developer (portfolio/career project) |
| Scope | Phases 1–5 (core), Phase 6+ (stretch, separate scope) |
| Related docs | [ROADMAP.md](./ROADMAP.md), [decisions/](./decisions/), [architecture/](./architecture/) |

**Priority key used throughout:** `P0` = must have, project is not credible without it. `P1` = strong showcase, expected of a senior-leaning candidate. `P2` = advanced showcase, high value but not required for MVP. `P3` = stretch/optional, only after P0–P2 are stable.

---

## 1. Executive Summary

Independent grocery and small retail stores run their operations on a patchwork of Excel sheets and aging, expensive-to-maintain POS/inventory software. Product registration is slow, stock counts drift from reality, billing at the counter is clunky, and store owners have no real visibility into their own sales trends — let alone how they compare to similar stores.

QuickStock is a multi-tenant SaaS platform that gives a grocery store fast product registration, fast billing, and accurate real-time inventory from day one, at a fraction of the cost of legacy systems. Each store's data is strictly isolated (tenant boundary enforced at the data-access layer, verified by tests), but the platform is architected so that, once enough stores are onboarded, aggregated and anonymized patterns can power AI-assisted business insights — a data network effect that a single-store desktop application could never provide.

This document is also an engineering demonstration artifact: it is written and built to be defensible in a senior-leaning .NET backend/full-stack interview, covering domain modeling, API design, database design, authentication/authorization, multi-tenancy, transactional correctness, concurrency, testing, containerization, cloud deployment, observability, and applied AI/RAG — each with a documented reason, not a résumé keyword.

## 2. Product Goals

**Business goals**
- Replace the "Excel + legacy POS" workflow for a small grocery store with a faster, cheaper, cloud-hosted alternative.
- Make product registration and checkout fast enough that a cashier or store owner prefers it to a spreadsheet.
- Establish a technical foundation (multi-tenant, data-isolated) that could plausibly onboard many stores without a rewrite.

**Technical goals**
- Ship a modular, well-tested, secure, observable .NET 10 backend with a Next.js client, backed by SQL Server, deployed to Azure via CI/CD ([ADR-008](./decisions/ADR-008-target-framework-net10-vs-net8.md): current LTS, not .NET 8).
- Demonstrate correct handling of the hard problems that separate CRUD apps from production systems: multi-tenant data isolation, transactional consistency, concurrency under contention, and permission-based authorization.
- Integrate AI/RAG as a genuine extension of the business system (tool-calling over real data, grounded document Q&A) rather than a bolted-on chatbot.

**Career/engineering demonstration goals**
- Close the developer's stated skill gaps: Azure, Docker, Kubernetes (conceptual), testing, system design, applied security, EF Core depth, and AI engineering.
- Produce a repository and running system that supports a full interview walkthrough: problem → architecture → decision → implementation → test → measurement → failure mode → what would change at scale.

## 3. Non-Goals

Explicitly out of scope for the 5-phase core plan (may be revisited post-MVP, see [ROADMAP.md](./ROADMAP.md) Phase 6+):

- **No cross-tenant analytics/data product.** The platform is built *capable of* this later, but no feature in Phases 1–5 aggregates or exposes data across tenants. This needs a real anonymization design and enough tenants to be meaningful — building it now would be speculative engineering.
- **No Avalonia desktop client** in the core phases. The backend is designed so a desktop client could be added later without duplicating business logic, but it is not built now.
- **No microservices.** The system is a modular monolith (see [ADR-001](./decisions/ADR-001-modular-monolith-vs-microservices.md)).
- **No Kubernetes cluster operations.** A K8s manifest set is produced as a documented artifact in Phase 4, but nothing runs on a live cluster.
- **No billing/subscription/payment-collection system** for onboarding paying store customers. `SubscriptionPlan` exists only as a schema stub for future use.
- **No offline-first/sync architecture.** The web client assumes network connectivity; offline resilience is a stretch concern, not core.
- **No multi-language/localization support.**
- **No self-service tenant sign-up flow with email verification/marketing funnel.** Tenant creation in the core phases is an authenticated admin operation, not a public sign-up page.

## 4. Personas

| Persona | Role | Primary needs |
|---|---|---|
| Store Owner / Super Admin | Owns a tenant (store), full control | Fast onboarding, visibility into sales/inventory, user management |
| Manager | Runs day-to-day operations | Approve purchases, view reports, manage staff-level access |
| Inventory Manager | Owns stock accuracy | Fast product registration, stock adjustments, receiving, low-stock alerts |
| Cashier / Sales User | Front counter | Fast, error-resistant checkout; minimal training needed |
| Procurement User | Supplier relationships | Create/track purchase orders, manage supplier data |
| Analyst / Viewer | Read-only stakeholder | Reports and dashboards, no mutation rights |
| Platform Admin (internal) | Operates the SaaS itself | Tenant provisioning, platform health, not a store employee |

## 5. Functional Requirements

Requirements are grouped by module. Each module lists its Phase and Priority.

### 5.1 Identity & Multi-Tenancy (Phase 1, P0)
- Tenant (Store) registration creates an isolated data boundary; all subsequent entities are implicitly scoped to it.
- User accounts belong to exactly one tenant (an employee cannot silently act across stores).
- Role assignment (Admin, Manager, Inventory Manager, Cashier, Procurement, Analyst) plus fine-grained permission claims (see §14).
- JWT access token + rotating refresh token session model.
- Deactivated users cannot authenticate, even with a valid refresh token.
- Audit event emitted for login, permission change, role change.

### 5.2 Product Catalog (Phase 1, P0)
- Products with SKU, barcode, category, brand, unit of measure, tax rate, price, active/inactive status.
- Category and brand management (simple hierarchies, not deep taxonomy).
- Barcode-first product lookup for fast registration and billing.
- Search/filter/sort/paginate on the product list (never load the full catalog into memory or to the client).
- Product registration must be completable in well under a minute for a typical item (few required fields, sane defaults, barcode scan pre-fills where possible) — this is a measured acceptance criterion, not a suggestion.

### 5.3 Inventory (Phase 2, P0)
- Per-store stock quantity per product (single-location in Phase 2; the schema anticipates multi-location, see §9).
- Stock movements: purchase receipt (+), sale (–), adjustment (± with reason and permission), transfer (stretch).
- Stock can never go negative through normal sale flow.
- Low-stock threshold per product; crossing it triggers a notification (background job, Phase 3).
- Every stock movement is individually auditable (who, what, when, why, resulting balance).

### 5.4 Suppliers & Purchasing (Phase 2, P0/P1)
- Supplier profile (contact, terms, status).
- Purchase order: header + line items, draft → submitted → approved → received/partially received → closed/cancelled lifecycle.
- Approval required above a configurable value threshold (P1).
- Receiving a purchase order increases inventory via the stock-movement mechanism, supports partial receipt.

### 5.5 Sales / Billing (Phase 2, P0)
- Cart-style sale creation: scan/search product, quantity, line discount, tax computed from product tax rate.
- Stock availability validated at checkout time, inside the same transaction that commits the sale (see §22).
- Payment capture (cash/card as a status field in Phase 2 — no real payment gateway integration in core scope).
- Invoice generated on successful sale.
- Checkout must be a fast, few-step flow — this is the platform's primary differentiator and is held to an explicit UX acceptance bar (see §7).
- Refund/return against an existing sale (P1), must reference the original transaction and re-validate business rules — cannot create stock out of nothing.

### 5.6 Customers (Phase 2, P1)
- Optional customer association on a sale (walk-in sales don't require one).
- Basic profile + purchase history view.

### 5.7 Reporting (Phase 3, P0/P1)
- Sales by day/month/product/category.
- Low-stock report, top products, average order value.
- All report endpoints are paginated and tenant-scoped; the PRD explicitly forbids "just load everything and aggregate in memory" implementations (see §24).

### 5.8 Notifications (Phase 3, P1)
- Low-stock notification, purchase-order-approved/received notification, generated by a background worker (§25), surfaced in the web UI.

### 5.9 Documents (Phase 4/5, P1/P2)
- Upload store policy/supplier documents to Blob Storage with metadata (type, size limits, tenant scope).
- Feeds the RAG pipeline in Phase 5.

### 5.10 AI Assistant (Phase 5, P1/P2)
- Tool-calling assistant answering data questions (inventory, sales) via authorized backend tools only.
- RAG-grounded document Q&A with citations.
- See §16, §27–29 (AI/RAG requirements) for full detail.

### 5.11 Audit (Phase 1–3, P0)
- Central audit log for all state-changing actions listed in §28.

## 6. User Stories

Representative stories per workflow (not exhaustive — full backlog lives alongside the issue tracker once implementation starts):

- *As a store owner*, I can register my store and create my first admin account, so I can start using the platform immediately.
- *As an inventory manager*, I can scan a barcode and fill in only price and initial stock to register a new product in under a minute.
- *As a cashier*, I can build a sale by scanning items, see the running total update live, and complete checkout in a handful of actions, so the line at the counter moves quickly.
- *As two cashiers*, if we both try to sell the last unit of a product at the same moment, exactly one sale succeeds and the other is rejected with a clear "out of stock" response — never a negative stock balance.
- *As a procurement user*, I can create a purchase order for a supplier, submit it for approval, and receive stock against it (including partial receipt) without manually re-typing quantities into an inventory screen.
- *As a manager*, I can view a monthly sales report broken down by product category, and the query returns in an acceptable time even as sales history grows, because it uses proper indexing rather than scanning the whole table.
- *As a store owner*, I can upload our return policy document and later ask "what is our return window?" and get an answer grounded in that document with a citation, not a hallucinated guess.
- *As a manager*, I can ask "which products are below reorder level?" and the assistant calls the real inventory API rather than inventing numbers.
- *As any user*, if I try to access another store's data (by guessing an ID, tampering with a token claim, etc.), the request fails — this is proven by an automated test, not just assumed.

## 7. Acceptance Criteria

Concrete, testable criteria for the P0 workflows:

| Workflow | Acceptance Criteria |
|---|---|
| Product registration | Required fields ≤ 5 for a minimal product; barcode scan auto-fills lookup where a match exists; save round-trip completes and the product is immediately visible in a paginated list without full page reload. |
| Checkout | Adding a line item is a single scan/search action; total updates without a full page reload; checkout completion (stock check → payment → invoice) is atomic — verified by an integration test that kills the process mid-flow in a test harness and asserts no partial state. |
| Concurrency | An integration test starts two simultaneous sale requests for the last unit of stock; exactly one succeeds; the other receives a documented "conflict/out of stock" error; final stock is never negative. |
| Tenant isolation | An integration test authenticates as Tenant A and attempts to read/write a Tenant B entity by ID; the request returns 404/403, never the data. |
| Report performance | The monthly sales report endpoint against a seeded dataset of realistic size (documented in the testing strategy) returns using an index seek, not a table scan — verified via execution plan inspection, not assumed. |
| AI tool call | Given a permitted user, "which products are below reorder level" invokes the `get_low_stock_items` tool and the response data matches what the reporting API itself would return for the same tenant. |
| RAG citation | A document Q&A answer includes at least one source document reference; if no relevant chunk is retrieved above the similarity threshold, the system says so rather than answering from general knowledge. |

## 8. Business Rules

Full catalog lives in [business/business-rules-catalog.md](./business/business-rules-catalog.md). Summary of the rules with the highest engineering consequence:

- Stock quantity must never go negative as a result of a sale (enforced at the transaction/concurrency level, not just UI validation).
- A sale cannot be created for an inactive product.
- Only users holding `purchase.approve` may approve a purchase order; the check happens server-side regardless of what the client UI shows.
- A refund must reference a real prior sale and cannot exceed the originally sold quantity.
- A deactivated user cannot authenticate even with a previously issued, still-unexpired refresh token (revocation is checked, not just token expiry).
- All entities except the platform-level tenant registry are implicitly scoped to exactly one tenant; there is no code path that queries across tenants inside a request handling a specific tenant's request.
- Audit records are append-only; nothing in the application layer exposes an update/delete on `AuditLog`.

## 9. Domain Model

Core aggregates, all tenant-scoped unless marked global:

```
Store (tenant root)
 ├─ User ─< UserRole >─ Role ─< RolePermission >─ Permission
 ├─ Category, Brand, Unit
 ├─ Product ─(category, brand, unit)
 ├─ InventoryItem (1:1 with Product per store/location)
 ├─ StockMovement (references InventoryItem, typed: Receipt/Sale/Adjustment/Transfer)
 ├─ Supplier ─< PurchaseOrder ─< PurchaseOrderItem
 │                    └─< GoodsReceipt ─< GoodsReceiptItem
 ├─ Customer
 ├─ SalesOrder ─< SalesOrderItem, ─ Payment, ─ Invoice
 ├─ AuditLog (append-only, references actor + entity)
 ├─ Notification
 └─ Document (metadata; blob content in storage; chunks/embeddings for RAG)

Global (not tenant-scoped):
 ├─ TenantRegistry / Store (the tenant root itself lives in a global table)
 ├─ SubscriptionPlan (stub, unused logic in core phases)
 └─ AiInteractionLog (platform-level AI observability, tenant-tagged but queried by platform admins only)
```

Full field-level detail, constraints, and relationships: [architecture/data-architecture.md](./architecture/data-architecture.md).

## 10. Data Model

See [architecture/data-architecture.md](./architecture/data-architecture.md) for the authoritative table-by-table definition (columns, keys, constraints, indexes, concurrency tokens). This PRD section defines the *rules* the data model must satisfy:

- Every tenant-owned table has a non-nullable `StoreId` foreign key and a composite index leading with `StoreId` on every query pattern that filters by store (which is nearly all of them).
- EF Core global query filters enforce `StoreId` scoping at the ORM level as defense-in-depth; the primary boundary is still explicit `WHERE`/parameterization, not the filter alone (see [ADR-002](./decisions/ADR-002-multi-tenancy-strategy.md)).
- Monetary values use `decimal`, never `float`/`double`.
- Stock-affecting tables (`InventoryItem`) carry a `RowVersion` concurrency token.
- Soft-delete (`IsActive`/status) is used for Products, Users, Suppliers — never a hard delete of entities with transaction history.
- `AuditLog` and `StockMovement` are append-only by convention (no `Update`/`Delete` DbSet operations exposed above the infrastructure layer for these types).

## 11. API Requirements

- RESTful, resource-oriented URLs (`/api/v1/products`, `/api/v1/sales-orders/{id}`).
- API versioning via URL segment (`/api/v1/...`) from day one — cheap now, expensive to retrofit.
- Standard HTTP status codes: 200/201/204 success, 400 validation, 401 unauthenticated, 403 unauthorized, 404 not found (including "not found because it belongs to another tenant" — never 403-leaking existence), 409 conflict (concurrency/business conflict), 500 unexpected (logged with correlation ID, generic message to client).
- Consistent response envelope for errors: `{ "type", "title", "status", "detail", "traceId", "errors": { field: [messages] } }` (RFC 7807 Problem Details).
- DTOs are distinct from EF Core entities in every direction — no entity is ever serialized directly to a client.
- List endpoints support pagination (offset-based for admin/report screens where total counts matter; keyset/cursor considered for the sales/audit history feed — decision documented at implementation time), filtering, sorting, and search via query parameters.
- `X-Correlation-Id` accepted from client or generated server-side, echoed in every response and included in every log line for that request.
- Optimistic concurrency surfaced via `If-Match`/`RowVersion` on mutating endpoints for stock-affecting resources.
- Idempotency key support on the checkout endpoint (a retried request with the same key does not create a duplicate sale).

## 12. Web Application Requirements

Next.js (App Router) + TypeScript client. Navigation: Dashboard, Products, Inventory, Purchases, Suppliers, Sales/Checkout, Customers, Reports, Documents, AI Assistant, Users, Audit, Settings — menu items rendered based on the authenticated user's permissions, not just hidden by CSS.

Required UX states for every data screen: loading, empty, error, and (for lists) paginated/filtered. Destructive actions (deactivate user, cancel purchase order, void sale) require explicit confirmation. Checkout screen is optimized for keyboard/barcode-scanner input, not mouse-first interaction. Visual design stays intentionally minimal (Tailwind + shadcn/ui defaults); engineering effort goes into correctness, loading/error handling, and responsiveness, not custom visual design.

## 13. Authentication & Authorization

Full flow documented in [architecture/authentication-flow.md](./architecture/authentication-flow.md). Summary:

- ASP.NET Core Identity provides user store + password hashing (PBKDF2/Argon2 via Identity's default hasher) — not reimplemented.
- Login issues a short-lived JWT access token (minutes) and a longer-lived, rotating refresh token (persisted, hashed at rest, single-use with reuse detection: reusing an already-rotated refresh token revokes the whole token family).
- Every JWT carries `sub` (user id), `store_id` (tenant), and a permission-derived claim set resolved at issue time.
- Authorization uses a custom `IAuthorizationHandler`/policy per permission string (e.g., `inventory.adjust`), not role string comparisons in controller code.
- `TenantContext` is resolved server-side from the validated JWT's `store_id` claim — never trusted from a client-supplied header — and injected into every tenant-scoped query.

## 14. Security Requirements

- Threat model and full mitigation table: [security/security-model.md](./security/security-model.md).
- Passwords hashed via ASP.NET Core Identity, never logged, never returned in any response.
- All database access parameterized via EF Core/typed SQL — no string-concatenated queries anywhere, including report/raw-SQL paths.
- CORS restricted to known client origins per environment.
- Rate limiting on authentication endpoints (login, refresh, register) to blunt credential-stuffing/brute-force.
- Secrets (connection strings, JWT signing key, OpenAI key) never in source control; local dev uses user-secrets/`.env`, cloud uses Azure Key Vault (Phase 4).
- File upload restrictions: allow-listed content types, size caps, virus-scan hook point documented even if not implemented in core scope.
- OWASP Top 10 walkthrough included in the security model doc, mapped to this system's specific mitigations (not generic advice).
- Cross-tenant data leakage is treated as a security bug class, not a functional bug — covered by dedicated integration tests, not just code review.

## 15. AI Requirements

Full detail: [architecture/ai-architecture.md](./architecture/ai-architecture.md). Summary requirements:

- OpenAI API (function/tool calling + structured outputs) as the primary LLM integration (see [ADR-005](./decisions/ADR-005-openai-direct-vs-azure-openai.md)).
- A fixed, backend-owned tool registry: `get_inventory`, `search_products`, `get_sales_summary`, `get_low_stock_items`, `get_purchase_order_status`, `get_supplier_status`, `get_customer_summary`. No tool executes arbitrary SQL or bypasses the same authorization checks a human user of that endpoint would face.
- Every tool call executes in the authenticated user's tenant/permission context — the LLM selects *which* tool and *what arguments*, the backend independently validates and executes.
- Structured JSON outputs for both tool-call arguments and final assistant responses that carry data (see §30 for schema).
- Maximum tool-call loop count per user turn (prevents runaway agent loops).

## 16. RAG Requirements

- Pipeline: Document upload → text extraction → cleaning → chunking → embedding (OpenAI embeddings API) → storage in SQL Server using the native `VECTOR` column type (Phase 5; see [ADR-003](./decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md)) → metadata (tenant, document, chunk index) → similarity retrieval → prompt assembly with retrieved context → grounded answer with citations.
- Retrieval is tenant-scoped: a similarity query always filters by `StoreId` before/alongside the vector search — a document from Store A can never surface in Store B's answer, regardless of vector similarity.
- If no chunk clears the similarity threshold, the assistant says it doesn't know rather than answering ungrounded.
- Every RAG answer includes chunk/document references sufficient for the user to verify the source.

## 17. Non-Functional Requirements

- **Performance:** P0 list/report endpoints must use index seeks for the primary access patterns at the dataset sizes defined in the testing strategy; no unbounded `SELECT *` into memory.
- **Scalability:** Stateless API (JWT-based auth, no server-side session) so horizontal scaling behind a load balancer requires no code change.
- **Reliability:** Critical multi-step workflows (checkout) are atomic; partial failure never leaves inconsistent state.
- **Maintainability:** Modular monolith with enforced dependency direction (verified by an architecture test, see [testing/testing-strategy.md](./testing/testing-strategy.md)).
- **Usability:** Checkout and product registration are held to explicit speed/step-count acceptance criteria (§7), not just "it works."
- **Security:** See §14.
- **Observability:** See §19.

## 18. Testing Strategy

Full strategy: [testing/testing-strategy.md](./testing/testing-strategy.md). Summary: unit tests (xUnit + FluentAssertions) for domain/business rules and calculations; integration tests (WebApplicationFactory + Testcontainers running real SQL Server) for API + database + auth + authorization + the checkout transaction + tenant-isolation proofs; a lightweight architecture test suite asserting the dependency-direction rule; AI-specific tests for tool-selection correctness and RAG retrieval/citation behavior on a fixed evaluation set.

## 19. Observability

Serilog structured logging with a correlation ID enriched on every request (from `X-Correlation-Id` or generated); log levels used meaningfully (not everything at `Information`); `/health/live` and `/health/ready` endpoints (readiness checks the DB connection); Application Insights in the Azure deployment (Phase 4) for request/dependency/exception telemetry. Every metric collected must answer a specific diagnostic question — no telemetry added "because observability."

## 20. Deployment Architecture

Local development: Docker Compose (API container + SQL Server container + Next.js dev server). Demo/staging: same containers, deployed via CI/CD to Azure (Container Apps or App Service — final choice recorded as an ADR once compared during Phase 4) with Azure SQL. Production-shape configuration (env-based, secrets via Key Vault) is used from Phase 1 onward even while running locally, so there is no "it worked locally but broke in the cloud" configuration cliff. Full detail: [architecture/deployment-architecture.md](./architecture/deployment-architecture.md), [operations/deployment.md](./operations/deployment.md).

## 21. CI/CD

GitHub Actions pipeline: restore → build → unit tests → integration tests (Testcontainers-based, run in CI) → Docker image build → (Phase 4) push + deploy to Azure. Pipeline fails the build on any failing test — there is no "tests are optional" stage.

## 22. Docker Strategy

Multi-stage Dockerfile for the API (SDK image for build/publish, ASP.NET runtime image for the final stage — smaller, no build tooling in production image). Non-root container user. `docker-compose.yml` for local dev wiring API + SQL Server + web with health-check-gated startup order. Full detail: [operations/local-development.md](./operations/local-development.md).

## 23. Azure Architecture

Candidate services and justification (finalized as ADRs during Phase 4): Azure Container Apps or App Service (compute), Azure SQL (database — same engine as local SQL Server, no migration surprises), Azure Blob Storage (documents), Azure Key Vault (secrets), Application Insights (observability). Each service entry in the eventual Azure ADR documents the cost-free local alternative and an explicit monthly cost estimate — no service is added without a stated reason and a stated cost.

## 24. Data / SQL Performance Strategy

Every P0/P1 list and report endpoint has a documented access pattern and a supporting index (leading column `StoreId` where applicable). Query performance work follows measure → identify bottleneck → change one thing → re-measure → document, using `SET STATISTICS IO, TIME ON` and execution plan inspection — not guesswork. Offset pagination is used for admin/report screens; the sales/audit activity feed is evaluated for keyset pagination once real access patterns exist. Full detail: [architecture/data-architecture.md](./architecture/data-architecture.md) §Indexing.

## 25. Background Jobs / Queues

A single in-process `IHostedService` worker pattern reading from a durable outbox/job table handles: low-stock notification generation, scheduled report pre-computation (P2). No message broker is introduced in the core phases (see [ADR-006](./decisions/ADR-006-background-processing-approach.md)) — the workload does not justify one yet, and adding Kafka/RabbitMQ purely for résumé value is explicitly rejected per the project's own anti-pattern list.

## 26. File Management

Documents (policy files, supplier agreements) are stored with metadata (owner tenant, type, size, upload timestamp) in the database and content in Blob Storage (local filesystem-backed abstraction in dev, Azure Blob in Phase 4) behind an `IStorageService` interface so the swap requires no business-logic change. Access is authorized per-tenant like every other resource.

## 27. AI Guardrails

- Fixed, backend-defined tool allow-list; the LLM cannot invent or request an arbitrary tool.
- Every tool executes with the calling user's real permission set — a `sales.create`-only user cannot get the assistant to approve a purchase order on their behalf.
- No tool ever accepts or constructs raw SQL.
- Output schema validation on structured responses before they reach the client.
- Maximum tool-call iterations per conversational turn to bound cost and prevent loops.
- Prompt-injection awareness: content retrieved from documents/tool results is treated as data, never as new instructions to the model (explicit system-prompt framing + tests with adversarial document content).

## 28. Auditability

Audited actions (actor, action, entity type/id, timestamp, relevant before/after where useful, correlation id): product price change, inventory adjustment, purchase order approval/receipt, sale creation/refund, user role/permission change, supplier change, document upload/delete, login. Audit records never contain secrets and are never mutated/deleted through the application layer.

## 29. Error Handling

Centralized exception-handling middleware maps domain/validation exceptions to the Problem Details envelope (§11); unexpected exceptions are logged with full detail server-side (including correlation ID) and returned to the client as a generic 500 with no stack trace or internal detail.

## 30. Architecture Decisions

Index of all ADRs: [decisions/ADR-000-index.md](./decisions/ADR-000-index.md). Locked for Phase 1: modular monolith, shared-schema multi-tenancy, SQL Server native vector, JWT+refresh auth, OpenAI direct, in-process background workers, `IMemoryCache` caching. Deferred to their relevant phase: Azure compute target, Semantic Kernel adoption.

## 31. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Scope too large for 4 hrs/day solo | Missed timeline, burnout | Desktop/K8s/multi-agent/cross-tenant-analytics deferred to Phase 6+; vertical slicing keeps the app demoable every phase |
| Cross-tenant data leak | Portfolio-credibility-destroying bug | Dedicated integration tests attempting cross-tenant access; global query filters as defense-in-depth |
| AI/RAG overengineering | Wasted time on infra with no business value | SQL Server native vector (no new DB), OpenAI direct (no Azure quota delay) |
| Azure cost creep | Unplanned expense | Per-service cost note + free/local alternative documented before adoption |
| "Data network" ambition distracts from MVP | Building unused features | Explicitly marked non-goal for Phases 1–5 (§3) |
| Generated code not actually understood | Can't defend it in interview | Every significant decision explained with alternatives/trade-offs/interview questions in this PRD and its ADRs |

## 32. Trade-offs

- **Modular monolith over microservices**: simpler to build/operate/reason about solo; sacrifices independent service scaling and deployment — acceptable because the traffic/team-size profile of a portfolio project (and most small SaaS at this stage) doesn't need it. See [ADR-001](./decisions/ADR-001-modular-monolith-vs-microservices.md).
- **Shared schema multi-tenancy over database-per-tenant**: much cheaper to operate and migrate; requires strict discipline (query filters + tests) to avoid cross-tenant leakage, which is the accepted trade-off given the tenant count expected in a portfolio/early-SaaS context. See [ADR-002](./decisions/ADR-002-multi-tenancy-strategy.md).
- **SQL Server native vector over a dedicated vector DB**: keeps infrastructure minimal and reuses an already-strong skill; sacrifices some of the retrieval sophistication a purpose-built vector database (e.g., Azure AI Search's hybrid search tooling) offers out of the box. See [ADR-003](./decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md).
- **OpenAI direct over Azure OpenAI initially**: faster to start, no quota/approval friction; sacrifices some Azure-native integration story until the documented later migration. See [ADR-005](./decisions/ADR-005-openai-direct-vs-azure-openai.md).

## 33. MVP Definition (P0)

The system is credible as an MVP when: a tenant can register and authenticate; products can be registered quickly with barcode support; inventory is accurate and concurrency-safe; a full purchase-to-receipt and sale-to-invoice workflow works atomically; tenant data isolation is proven by tests; the system runs reproducibly via Docker Compose; and core business logic has unit/integration test coverage. This corresponds to Phases 1–2 plus the tenant-isolation and testing requirements woven through them.

## 34. Showcase Scope (P1/P2)

Security hardening, observability, SQL performance measurement, Azure cloud deployment with Key Vault-managed secrets, and the AI tool-calling + RAG assistant. This corresponds to Phases 3–5.

## 35. Stretch Scope (P3 / Phase 6+)

Avalonia desktop billing client, live Kubernetes deployment, Redis (only if a real need emerges), MCP exposure of the tool registry, multi-agent orchestration, cross-tenant aggregate analytics/data product, self-service tenant sign-up and subscription billing.

## 36. 5-Phase Implementation Roadmap

Full detail in [ROADMAP.md](./ROADMAP.md). Summary: Phase 1 Foundation/Identity/Multi-Tenant Core + Catalog · Phase 2 Inventory/Purchasing/Sales core workflows · Phase 3 Security/Performance/Observability · Phase 4 Azure Cloud Deployment · Phase 5 AI/RAG + interview polish.

## 37. Definition of Done

The project is "done" (for the 5-phase core scope) when every P0/P1 item above is implemented, tested per §18, deployed and reachable per §20, documented per the `docs/` tree, and the developer can walk through the full interview narrative in §"Final Principle" of the original planning brief: business requirement → domain model → API → database strategy → security → tests → containerization → deployment → monitoring → performance → AI integration, for this specific system, from memory, without reading the code first.
