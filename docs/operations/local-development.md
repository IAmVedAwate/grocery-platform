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
cp .env.example .env                          # fill in local values — never commit .env
cd deployment && docker compose --env-file ../.env up -d --build   # starts SQL Server + API
cd ../backend && dotnet restore
cd ../web && npm install
```

Backend database migrations are applied automatically on API startup in the Development environment (`dbContext.Database.MigrateAsync()` in `Program.cs`, gated on `IsDevelopment()` — never runs this way in staging/production, see [deployment-architecture.md](../architecture/deployment-architecture.md)), so a fresh clone reaches a working schema with no manual migration step.

## Running the Backend

Either via Docker Compose (above), or directly on the host:

```
cd backend/src/Api
dotnet run
```

Requires `DB_CONNECTION_STRING` and `JWT_SIGNING_KEY` to be available — via `dotnet user-secrets` (recommended for solo local dev; see below) or exported environment variables. API listens on `http://localhost:5292` (`https://localhost:7223`) by default; see `backend/src/Api/Properties/launchSettings.json`. Swagger/OpenAPI UI is enabled in the Development environment only.

### Local secrets via `dotnet user-secrets`

```
cd backend/src/Api
dotnet user-secrets set "JWT_SIGNING_KEY" "<a long random string>"
dotnet user-secrets set "DB_CONNECTION_STRING" "Server=localhost,1434;Database=QuickStock;User Id=sa;Password=<matches MSSQL_SA_PASSWORD>;TrustServerCertificate=True"
```

Stored outside the repo (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows), never committed — see [security-model.md](../security/security-model.md).

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

Defined in [`deployment/docker-compose.yml`](../../deployment/docker-compose.yml):

| Service | Purpose | Notes |
|---|---|---|
| `api` | ASP.NET Core backend | Multi-stage Dockerfile (`backend/src/Api/Dockerfile`); runs as the non-root `app` user built into Microsoft's runtime image; waits on `sqlserver`'s health check; exposes `http://localhost:5292` |
| `sqlserver` | SQL Server 2025 | Host port **1434**, not 1433 (leaves the default free for a local install or another project); data persisted in the `sqlserver-data` volume |

The Next.js client is not containerized yet — `npm run dev` in `web/` is the local dev path (see [PRD.md §3 Non-Goals](../PRD.md#3-non-goals) territory: a `web` compose service is a reasonable Phase 3+ addition once the app has more than a scaffold to containerize).

## Common Issues

Documented as they're actually encountered during implementation — see [troubleshooting.md](./troubleshooting.md), which starts empty and grows with real incidents rather than speculative content.

## Seed Data

A `database/` seed script populates a demo tenant with representative products/suppliers/customers so the UI is usable immediately after setup, plus a larger synthetic dataset (documented in [testing/testing-strategy.md](../testing/testing-strategy.md)) used specifically for the Phase 3 performance work.
