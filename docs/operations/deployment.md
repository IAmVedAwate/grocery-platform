# Deployment (Phase 4 target — filled in as implemented)

This document will describe, step by step, exactly how to reproduce the cloud deployment from a fresh clone. It is written progressively as Phase 4 is implemented, not speculatively in advance — a deployment doc that describes an untested process is worse than no doc.

## Planned Structure (to be completed during Phase 4)

1. **Prerequisites** — Azure subscription, CLI tools, required permissions.
2. **Provisioning** — the exact resources created (compute target, Azure SQL, Blob Storage, Key Vault, Application Insights), with the reasoning already recorded in [architecture/deployment-architecture.md](../architecture/deployment-architecture.md) and the Azure ADR (added during Phase 4).
3. **Secrets** — how each secret gets into Key Vault and how the app retrieves it (Managed Identity flow, explained concretely).
4. **CI/CD** — what the GitHub Actions deploy stage actually does, referencing `.github/workflows/`.
5. **Verification** — how to confirm a deploy succeeded (health endpoint checks, a smoke-test script).
6. **Rollback** — how to revert to the previous known-good deployment.
7. **Cost** — actual observed monthly cost at demo scale, compared against the estimate in the Azure ADR.

## Status

Not yet implemented — Phase 4 of [ROADMAP.md](../ROADMAP.md). Local development is fully documented in [local-development.md](./local-development.md).
