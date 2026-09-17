# .github/workflows

GitHub Actions CI/CD pipeline: restore → build → unit tests → integration tests (Testcontainers) → architecture tests → Docker image build → (Phase 4) push + deploy to Azure. See [docs/PRD.md §21](../../docs/PRD.md#21-cicd) and [docs/architecture/deployment-architecture.md](../../docs/architecture/deployment-architecture.md).

Workflow YAML added alongside the first backend code in Phase 1.
