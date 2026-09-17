# Local Development

## Prerequisites

- .NET 10 SDK (see [ADR-008](../decisions/ADR-008-target-framework-net10-vs-net8.md))
- Node.js (LTS) + npm/pnpm for the Next.js client
- Docker Desktop (API container + SQL Server container + Testcontainers for integration tests)
- An OpenAI API key (only required once Phase 5 AI features are being developed/run)

## First-Time Setup

```
git clone <repo>
cd grocery-platform
cp .env.example .env         # fill in local values — never commit .env
docker compose up -d         # starts SQL Server + API (once scaffolded)
cd backend && dotnet restore
cd ../web && npm install
```

Backend database migrations are applied automatically on API startup in the local environment (via `dbContext.Database.Migrate()` behind an environment check), so a fresh clone reaches a working schema with no manual migration step.

## Running the Backend

```
cd backend/src/Api
dotnet run
```

API listens on the port defined in `appsettings.Development.json` / `.env`. Swagger/OpenAPI UI is enabled in the Development environment only.

## Running the Web Client

```
cd web
npm run dev
```

Configured (via `.env.local`, mirroring `.env.example`) to point at the local API's base URL.

## Running Tests

```
cd backend
dotnet test tests/Unit
dotnet test tests/Integration     # requires Docker running (Testcontainers spins up SQL Server)
dotnet test tests/Architecture
```

## Docker Compose Services

| Service | Purpose | Notes |
|---|---|---|
| `api` | ASP.NET Core backend | Multi-stage Dockerfile; non-root user; waits on `sqlserver` health check |
| `sqlserver` | SQL Server 2025 | Local dev + integration test target; data persisted in a named volume |
| `web` | Next.js client (optional container; `npm run dev` also works standalone) | |

## Common Issues

Documented as they're actually encountered during implementation — see [troubleshooting.md](./troubleshooting.md), which starts empty and grows with real incidents rather than speculative content.

## Seed Data

A `database/` seed script populates a demo tenant with representative products/suppliers/customers so the UI is usable immediately after setup, plus a larger synthetic dataset (documented in [testing/testing-strategy.md](../testing/testing-strategy.md)) used specifically for the Phase 3 performance work.
