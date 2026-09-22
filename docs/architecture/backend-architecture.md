# Backend Architecture

## Solution Structure

```
backend/
├── src/
│   ├── Api/              ASP.NET Core host: controllers/minimal APIs, middleware, DI wiring, DTOs
│   ├── Application/      Use cases per module (e.g. Sales.CreateSaleOrder), orchestration, validation,
│   │                     interfaces the Infrastructure layer implements (IRepository, IStorageService)
│   ├── Domain/            Entities, value objects, domain business rules, domain events — no EF Core,
│   │                     no ASP.NET Core, no external SDK references
│   ├── Infrastructure/    EF Core DbContext + configurations + migrations, repository implementations,
│   │                     Blob Storage adapter, Gemini adapter (via Microsoft.Extensions.AI's
│   │                     provider-neutral IChatClient/IEmbeddingGenerator — see ADR-009), Identity integration
│   └── Shared/            Cross-cutting: Problem Details error model, correlation ID, audit-log writer,
│                          permission constants
└── tests/
    ├── Unit/              Domain + Application logic, no database, no HTTP
    ├── Integration/        WebApplicationFactory + Testcontainers SQL Server — real API + real DB
    └── Architecture/       NetArchTest-style rules asserting the dependency direction below
```

## Dependency Direction (enforced, not just documented)

```
Api  ──depends on──▶  Application  ──depends on──▶  Domain
Infrastructure  ──depends on──▶  Application (interfaces)  +  Domain (entities)
```

- `Domain` has zero outward dependencies — no EF Core, no ASP.NET Core, no AI SDK. It is pure C#: entities and business rules that could be unit tested without a running process.
- `Application` depends only on `Domain` and defines the interfaces (`IProductRepository`, `IStorageService`, `IUnitOfWork`) that `Infrastructure` implements — this is the Dependency Inversion half of SOLID doing real work, not decoration. The AI tool registry (`AiTools`) is the one exception worth naming: it lives in `Infrastructure`, not behind an `Application`-level interface, because it depends on `Microsoft.Extensions.AI`'s own already-provider-neutral abstractions (`IChatClient`, `IEmbeddingGenerator`) directly — wrapping those in a second, project-specific interface would be indirection with no real benefit (see [ADR-009](../decisions/ADR-009-llm-provider-gemini-direct.md)).
- `Infrastructure` is the only layer allowed to reference EF Core, Azure SDKs, or the Gemini SDK (`Google.GenAI`).
- `Api` wires everything together via DI at startup and exposes controllers/endpoints; it contains no business logic itself — a controller action calls into `Application`, nothing more.

An `Architecture` test project asserts these rules automatically (e.g., "no type in `Domain` may reference `Microsoft.EntityFrameworkCore`") so the boundary can't silently erode as the codebase grows — this is the concrete, testable version of "Clean Architecture," not just a folder-naming convention (per the planning brief's explicit warning against treating Clean Architecture as "four folders only").

## Module Organization Within Layers

Within `Application` and `Domain`, code is organized by business module first, technical concern second:

```
Application/
├── Catalog/     (CreateProduct, UpdateProduct, ListProducts, ...)
├── Inventory/   (AdjustStock, RecordStockMovement, ...)
├── Sales/       (CreateSaleOrder, CompleteCheckout, RefundSale, ...)
├── Purchasing/
├── Identity/
├── Reporting/
└── Ai/
```

This keeps a module's use cases discoverable together, and makes a future service-extraction (should one ever be justified — see [ADR-001](../decisions/ADR-001-modular-monolith-vs-microservices.md)) a matter of lifting a folder, not untangling one.

## Cross-Module Communication

Modules that need to react to another module's action (e.g., Notifications reacting to Inventory crossing a low-stock threshold) do so through in-process domain events raised by the `Domain` layer and handled within `Application` — not direct repository calls into another module's tables. This keeps module boundaries meaningful even though everything currently runs in one process and one database.

## Request Lifecycle (typical mutating request)

```
HTTP request → correlation-id middleware → auth middleware (JWT validation,
TenantContext resolved from claims) → authorization policy check (permission
claim) → controller → Application use case (validates business rules, opens
a transaction if multi-step) → Domain entities apply rules → Infrastructure
persists via EF Core (tenant query filter applies) → Audit entry written →
response mapped to DTO → Problem Details on any thrown domain/validation
exception, generic 500 + logged detail on anything unexpected.
```
