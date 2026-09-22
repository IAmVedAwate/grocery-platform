# Skills & Capability Inventory

A recruiter-facing reference, not a checkpoint narrative like [phase-1](./phase-1-checkpoint-explanation.md)/[phase-2](./phase-2-checkpoint-explanation.md) — this answers three separate questions: what architecture and design patterns does this project actually use (named specifically, not vaguely), what's genuinely ready to show a recruiter today, and what's an honest, named gap rather than something quietly hoped nobody asks about. Verified against the real code, not recalled from memory — re-verify before quoting a specific claim in an interview if enough time has passed that the code may have moved.

---

## 1. Architecture

Architecture isn't one label — this project stacks three separate decisions, each at a different level, and being able to name which decision operates where is itself a strong interview answer.

| Architecture style | What it means | Interview importance | Used here? |
|---|---|---|---|
| **Modular Monolith** | One deployable process, internal module boundaries enforced by code/tooling | ⭐⭐⭐ | ✅ The primary label for this project |
| **Clean Architecture** (Uncle Bob) | Dependencies point inward toward Domain; Domain has zero framework references | ⭐⭐⭐ | ✅ Influenced by — `Api → Application → Domain`, `Infrastructure → abstractions`, enforced by `NetArchTest` in `tests/Architecture` |
| **Onion Architecture** | Same dependency-inward idea as Clean, different diagram/vocabulary | ⭐⭐ | ✅ Same evidence as Clean — effectively synonymous here |
| **Hexagonal / Ports & Adapters** | Core logic behind "ports" (interfaces); infra plugs in as "adapters" | ⭐⭐ | ✅ `IProductRepository`/`IStorageService`/`IColorExtractionService` are the ports; EF Core, local-filesystem, and ONNX-backed implementations are the adapters |
| **Multi-tenant SaaS architecture** (shared-DB/shared-schema vs. DB-per-tenant vs. schema-per-tenant) | How one system serves many isolated customers | ⭐⭐⭐ (central for SaaS roles) | ✅ Shared database, shared schema, row-level isolation via EF Core global query filters ([ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md)) |
| Layered / N-Tier | Presentation → Business → Data, no strict dependency-inversion rule | ⭐⭐ | Not this project's label — stricter than plain N-Tier |
| Vertical Slice | Organize by feature/use-case, not technical layer | ⭐⭐ | ❌ Organizes layer-then-module, not slice-then-feature |
| CQRS | Separate models/paths for reads vs. writes | ⭐⭐⭐ | ❌ One model serves both; no MediatR, no separate read model |
| Event-Driven Architecture | Services communicate via published events, not direct calls | ⭐⭐⭐ | ❌ No event bus/broker anywhere |
| Microservices | Independently deployable services per bounded context | ⭐⭐⭐ | ❌ Explicitly rejected — [ADR-001](../decisions/ADR-001-modular-monolith-vs-microservices.md) documents why |
| Event Sourcing, Serverless, SOA | — | ⭐ | ❌ None used, not relevant to this system's shape |

**The precise answer to "what architecture is this":** a **modular monolith**, internally structured per **Clean/Onion/Hexagonal** principles (three names for the same dependency-inversion idea), serving as a **multi-tenant SaaS** via the shared-schema strategy — three stackable decisions, not one word.

---

## 2. Design Patterns Actually In This Codebase

**GoF patterns** relevant to .NET backend work (several classic GoF patterns — Prototype, Flyweight, Memento, Visitor — essentially never come up in this kind of system and aren't listed as gaps):

| Pattern | ⭐ | Used here? |
|---|---|---|
| Strategy | ⭐⭐⭐ | ✅ `PermissionAuthorizationHandler`'s policy evaluation; exception→status mapping in `AppExceptionHandler` |
| Chain of Responsibility | ⭐⭐⭐ | ✅ ASP.NET Core's middleware pipeline itself (`CorrelationIdMiddleware` → exception handler → auth → rate limiter → controllers) |
| Adapter | ⭐⭐ | ✅ `ProductRepository`/`ReportingRepository` etc. adapt EF Core's `DbContext` to Application's own interfaces; `OnnxColorExtractionService` adapts `InferenceSession` the same way |
| Facade | ⭐ | ✅ Each `*ApplicationService` (e.g. `SalesApplicationService`) is a facade over multiple repositories + domain calls |
| Factory Method | ⭐⭐ | ✅ Public constructors on `Product`, `SalesOrder`, etc. are the only valid creation path, enforcing invariants at construction |
| Singleton | ⭐⭐ | ✅ `IAuthorizationPolicyProvider`, `IMemoryCache`, and — as of the color-extraction feature — `IColorExtractionService`, deliberately singleton so the ONNX model loads once, not per request |
| State | ⭐⭐ | ✅ `PurchaseOrderStatus`/`SalesOrderStatus` + guarded transition methods (`Approve()`, `Cancel()`, `RefundLines()`) |
| Command, Mediator, Decorator, Observer, Builder | ⭐⭐–⭐⭐⭐ | ❌ Not used — no MediatR/CQRS, no pub-sub |

**.NET / enterprise idioms** (Martin Fowler's *PoEAA* + ASP.NET Core conventions — most of what .NET interviews actually ask about, and mostly not in the GoF book at all):

| Pattern | ⭐ | Used here? |
|---|---|---|
| Repository | ⭐⭐⭐ | ✅ Every module |
| Unit of Work | ⭐⭐⭐ | ✅ `IUnitOfWork` wrapping `GroceryDbContext.SaveChangesAsync` |
| Dependency Injection / IoC | ⭐⭐⭐ | ✅ Everywhere |
| Options Pattern | ⭐⭐ | ✅ JWT/CORS/rate-limit config |
| Middleware Pipeline | ⭐⭐⭐ | ✅ `CorrelationIdMiddleware`, `IExceptionHandler` |
| DTO | ⭐⭐⭐ | ✅ Entities never serialized directly |
| Rich Domain Model (vs. Anemic) | ⭐⭐⭐ | ✅ Private setters, behavior on the entity (`InventoryItem.Decrease()`, `Product.SetImage()`) |
| Guard Clause | ⭐⭐ | ✅ `DomainRuleGuard.Run(...)`, constructor-level checks throughout Domain |
| Optimistic Concurrency | ⭐⭐⭐ | ✅ SQL Server `ROWVERSION` on `InventoryItem` |
| Cache-Aside | ⭐⭐⭐ | ✅ `IMemoryCache` for Category reads |
| Idempotency Key | ⭐⭐ | ✅ `Idempotency-Key` header on checkout |
| Claims/Policy-based Authorization | ⭐⭐⭐ | ✅ Since the staff-management feature, **not role-derived at read time at all** — see §3 |
| Row-Level Security / Global Query Filter | ⭐⭐⭐ | ✅ EF Core `HasQueryFilter` per tenant-owned entity |
| Health Check, Rate Limiter, Background Worker patterns | ⭐⭐ | ✅ `/health/*`, auth rate limiting, `LowStockNotificationWorker` |
| **Pluggable storage backend** (Strategy applied to infra) | ⭐⭐ | ✅ `IStorageService` → `LocalFileStorageService` today, Azure Blob a clean future swap-in with zero Application/Api changes |
| Outbox pattern | ⭐⭐⭐ | ❌ PRD mentions it aspirationally; the actual worker is a simpler polling sweep — a documented simplification, not an oversight |
| Circuit Breaker / Retry (Polly) | ⭐⭐⭐ | ❌ No resilience library anywhere |
| Saga, Specification | ⭐⭐ | ❌ Not used |

---

## 3. What's Actually Ready to Show a Recruiter

✅ = solidly demonstrated · ❌ = not present · ⚠️ = partial, with exactly what's missing named.

### C# / backend fundamentals
| Item | Status | Note |
|---|---|---|
| C# strengthening | ✅ | Records, primary constructors, pattern matching, nullable reference types — C# 13/.NET 10 throughout |
| DI, EF Core, middleware, exception handling, logging | ✅ | Serilog structured logging; centralized `IExceptionHandler` → RFC 7807 Problem Details |
| SOLID | ✅ | Concretely: SRP via per-module services, DIP via Application-defines/Infrastructure-implements, ISP via small focused repository interfaces — demonstrated by the layering, not a single file to point at |
| validation | ✅ | Hand-rolled (domain constructor guards + `ValidationAppException`), **not** FluentValidation — say that precisely if asked |
| API design | ✅ | REST, `/api/v1/...` versioning, pagination, consistent error envelope, multipart file upload (`PUT /products/{id}/image`) |
| async/concurrency | ✅ | async/await throughout, **plus** a real concurrency test (two simultaneous sales for the last unit of stock) |

### SQL Server internals
| Item | Status | Note |
|---|---|---|
| indexes, execution plans, query optimization, EF Core generated SQL | ✅ | The Phase 3 SQL performance pass — 76% fewer logical reads, measured before/after, documented in [sql-performance-pass.md](../architecture/sql-performance-pass.md) |
| tracking vs. NoTracking | ✅ | `AsNoTracking()` consistently across every read-only repository method |
| N+1 queries | ⚠️ | `.Include()` used in two repositories shows awareness; no dedicated "found and fixed an N+1" story the way the performance pass has one for scans-vs-seeks |
| transactions, isolation levels, locking, deadlocks, stored procedures, compiled queries | ❌ | Genuinely not demonstrated anywhere — everything goes through EF Core/LINQ with implicit per-`SaveChangesAsync` transactions; no explicit `BeginTransaction`/isolation-level/locking-hint/`sp_getapplock` code exists |

### Security
| Item | Status | Note |
|---|---|---|
| JWT, refresh tokens | ✅ | Rotating, with reuse-detection (token-family revocation) |
| Authorization model | ✅ | **Not RBAC** — permission keys are assigned directly per user (`UserPermissionEntity`), editable any time from Settings → Staff; a role is only ever consulted once, as the store-registration admin's starting bundle. See [authentication-flow.md](../architecture/authentication-flow.md#staff-accounts--per-user-permissions) |
| secure configuration | ✅ | `.env`/`.env.local`, nothing in source control; Azure Key Vault is the documented Phase 4 plan, not yet built |
| password reset | ❌ | Deferred by explicit choice — no SMTP/email integration exists yet to build a real reset-link flow against |

### Testing
| Item | Status | Note |
|---|---|---|
| unit testing | ✅ | xUnit, 38 tests |
| integration testing | ✅ | Testcontainers + `WebApplicationFactory`, 51 tests — real SQL Server, real ONNX model, real file I/O, never mocked |
| test database | ✅ | Real SQL Server 2025 via Testcontainers |
| testable architecture | ✅ | DI + interfaces + enforced architecture tests |
| **mocking** | ❌ | No Moq/NSubstitute/FakeItEasy anywhere. Real gap: the project's "real infra, not mocks" philosophy is defensible, but it means there's no code demonstrating mocking a dependency specifically |

### Docker / DevOps
| Item | Status | Note |
|---|---|---|
| Docker | ✅ | Multi-stage `Dockerfile`, non-root `$APP_UID`, `HEALTHCHECK` |
| Docker Compose, container networking, environment variables | ✅ | `sqlserver` + `api` services, health-check-gated startup order, confirmed cross-container networking |
| Azure (App Service/Container Apps, SQL, Blob Storage, Key Vault), Application Insights, deployment pipeline | ❌ | Phase 4, not started — `.github/workflows/` has only a README placeholder |

### Business modules
| Item | Status |
|---|---|
| authentication system | ✅ |
| **staff accounts & per-user permissions** | ✅ (new — Settings → Staff, create/edit/deactivate, real permission checklist) |
| inventory system | ✅ |
| order system | ✅ (sales orders and purchase orders) |
| notification system | ✅ (background low-stock sweep) |
| **product images** | ✅ (new — upload/replace/remove, local storage abstraction, ready to swap to Azure Blob) |
| **product color recognition** | ✅ (new — a real ONNX model identifies a product's dominant color on upload; used as a 50%-opacity card background for color-based browsing) |
| document system (policy/supplier documents feeding RAG) | ❌ | Phase 5, not started — distinct from product images, which *is* done |
| AI assistant (LLM tool-calling, RAG, embeddings) | ❌ | Phase 5, not started |

### System design concepts
| Item | Status | Note |
|---|---|---|
| monolith vs. modular monolith vs. microservices | ✅ | Chosen and justified with trade-offs in [ADR-001](../decisions/ADR-001-modular-monolith-vs-microservices.md) |
| caching | ✅ | `IMemoryCache`, cache-aside |
| **on-device ML inference** | ✅ | ONNX Runtime running a real trained model in-process — a genuinely different skill from calling an LLM API, worth naming separately from "AI assistant" below |
| queues | ❌ | Explicitly rejected, not skipped — [ADR-006](../decisions/ADR-006-background-processing-approach.md) documents why an in-process worker was chosen over Kafka/RabbitMQ |
| scaling | ⚠️ | Addressed conceptually (stateless JWT auth) but never load-tested or deployed at scale |
| failure handling | ⚠️ | Concurrency-safe checkout, idempotency keys, centralized exception handling — no Polly-based retry/circuit-breaker |
| observability | ✅ | Serilog, correlation IDs, `/health/live` + `/health/ready`; Application Insights is the one piece still Phase 4 |

### AI / LLM (Phase 5 — all deferred, decisions already made)
LLM basics, OpenAI API, structured output, function calling, embeddings, RAG, vector search, retrieval quality, prompt engineering, Semantic Kernel, agents, guardrails, evaluation — ❌ all, zero code written. Locked decisions to explain if asked *why not yet*: OpenAI direct over Azure OpenAI ([ADR-005](../decisions/ADR-005-openai-direct-vs-azure-openai.md)), SQL Server native `VECTOR` type over a dedicated vector DB ([ADR-003](../decisions/ADR-003-sql-server-native-vector-vs-pgvector-vs-azure-ai-search.md)).

---

## 4. Real Gaps, Named Honestly

Not hidden, not excused — a plain list to close or consciously accept before interviews start.

1. **No audit trail for a product price change.** Login, permission changes, inventory adjustments, purchase approvals, and sales are all audited; `ProductApplicationService.UpdateAsync` (price/tax changes) is not. A real, easy-to-close gap, not a documented trade-off.
2. **No mocking example anywhere.** Defensible philosophy (real DB/model over mocks), but worth having *one* deliberate example if a mocking question comes up.
3. **No explicit transaction/isolation/locking demonstration.** Everything relies on EF Core's implicit per-`SaveChangesAsync` transaction; no `BeginTransaction`, isolation level, or pessimistic lock hint exists to point at.
4. **No CI/CD pipeline.** `.github/workflows/` is a placeholder.
5. **No cloud deployment.** Phase 4, openly deferred.
6. **No AI/RAG code.** Phase 5, openly deferred — architecture decisions already made, nothing built.
7. **No resilience library (Polly).** Failure handling exists at the business-logic level (idempotency, concurrency safety) but not at the infrastructure/network level.

---

Related: [ROADMAP.md](../ROADMAP.md) for phase-by-phase scope, [phase-1](./phase-1-checkpoint-explanation.md)/[phase-2](./phase-2-checkpoint-explanation.md) checkpoint docs for the interview-narrative version of Phases 1–2, [decisions/](../decisions/) for every ADR referenced above.
