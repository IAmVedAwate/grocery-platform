# .github/workflows

`ci.yml`: restore → build → unit tests → architecture tests → integration tests (Testcontainers, real SQL Server) → frontend lint/build → Docker image build. No deploy step yet — that's the Phase 4 addition (push + deploy to Azure), not built. See [docs/PRD.md §21](../../docs/PRD.md#21-cicd) and [docs/architecture/deployment-architecture.md](../../docs/architecture/deployment-architecture.md).

Every step was run locally with the exact same commands before this file was committed — including the Docker build from the repo root and `npm run lint`, which caught a real pre-existing lint error (`product-image.tsx`) that would otherwise have failed the very first CI run.
