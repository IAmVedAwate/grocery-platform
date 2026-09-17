# Infrastructure

EF Core `DbContext` + entity configurations + migrations, repository implementations, tenant query-filter wiring, Blob/local storage adapter, OpenAI adapter, ASP.NET Core Identity integration. The only layer allowed to reference EF Core, Azure SDKs, or the OpenAI SDK.

See [docs/architecture/backend-architecture.md](../../../docs/architecture/backend-architecture.md) and [docs/architecture/data-architecture.md](../../../docs/architecture/data-architecture.md).

Not yet scaffolded — first code lands in Phase 1 (Slice 0) per [docs/ROADMAP.md](../../../docs/ROADMAP.md).
