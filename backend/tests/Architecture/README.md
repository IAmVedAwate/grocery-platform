# Architecture Tests

Asserts the dependency-direction rule in [docs/architecture/backend-architecture.md](../../../docs/architecture/backend-architecture.md) — e.g., no type in `Domain` may reference EF Core, ASP.NET Core, or the OpenAI SDK. Fails the build if the boundary erodes.
