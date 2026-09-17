# Api

ASP.NET Core host project. Controllers/minimal API endpoints, middleware (correlation ID, exception handling, auth), DI composition root, request/response DTOs, Swagger/OpenAPI setup.

Contains no business logic — every action delegates to an `Application` use case. See [docs/architecture/backend-architecture.md](../../../docs/architecture/backend-architecture.md).

Not yet scaffolded — first code lands in Phase 1 (Slice 0) per [docs/ROADMAP.md](../../../docs/ROADMAP.md).
