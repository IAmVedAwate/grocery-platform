# Deployment Architecture

## Environments

| Environment | Where | Database | Secrets | Purpose |
|---|---|---|---|---|
| Local | Docker Compose on developer machine | SQL Server container | `.env` / user-secrets (never committed) | Day-to-day development |
| CI | GitHub Actions runner | SQL Server via Testcontainers | GitHub Actions secrets | Automated build/test on every push |
| Demo/Staging | Azure (Phase 4) | Azure SQL | Azure Key Vault | Public-reachable demo for interviews/portfolio |
| Production (conceptual) | Azure | Azure SQL | Azure Key Vault | Documented target; not operated as a paying-customer product in core scope |

Configuration is environment-driven from Phase 1 onward (`appsettings.{Environment}.json` + environment variables + a documented secrets provider per environment) specifically so there is no "worked locally, broke in the cloud" configuration cliff when Phase 4 arrives — see [operations/local-development.md](../operations/local-development.md) and [operations/deployment.md](../operations/deployment.md).

## Local (Phases 1–3)

```
docker-compose.yml
├── api        (multi-stage Dockerfile: build with SDK image, run on ASP.NET runtime image, non-root user)
├── sqlserver  (official SQL Server container image, health-checked before api starts)
└── web        (Next.js dev server, or its own container for a closer-to-prod check)
```

## Cloud (Phase 4)

```
GitHub Actions (on push to main)
  → restore, build, unit tests, integration tests (Testcontainers)
  → docker build (api image)
  → push image to a container registry
  → deploy to Azure compute (App Service or Container Apps — decided via a
    Phase 4 ADR once both are actually compared for this workload)
  → Azure SQL (schema migrated via EF Core migrations as a deploy step)
  → secrets pulled from Azure Key Vault via Managed Identity (no connection
    string or API key in App Service configuration as plain text)
  → Application Insights receives request/dependency/exception telemetry
```

## Kubernetes Artifact (Phase 4, documented not deployed)

A manifest set (`Deployment`, `Service`, `ConfigMap`, `Secret`, readiness/liveness probes tied to `/health/ready` and `/health/live`) is produced and explained in the repository as a demonstration of understanding — per the original brief, the goal is "understand what Kubernetes solves and be able to deploy/describe a simple containerized workload," not operate a live cluster. Running it against a real cluster (e.g., AKS or a local kind cluster) is explicitly Phase 6+.

## Rollback / Failure Handling

Deployment failures are handled by redeploying the previous known-good image tag (standard for this scale); no blue/green or canary infrastructure is built in core scope — noted as a stretch improvement, not a gap that undermines the MVP.

## Cost Awareness

Every Azure service introduced in Phase 4 is documented in the eventual Azure ADR with: what problem it solves, its approximate monthly cost at demo scale, and the free/local alternative used during development — consistent with [PRD §23](../PRD.md#23-azure-architecture) and the original brief's explicit instruction that cost-awareness is itself part of professional engineering.
