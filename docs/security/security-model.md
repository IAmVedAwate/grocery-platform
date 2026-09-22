# Security Model

## Threat Model Summary

The highest-consequence threat for this system is **cross-tenant data access** — a bug that lets one grocery store see or modify another's data is both a real-world business failure and a credibility-destroying bug in a portfolio project. Every other control below is standard practice; this one gets dedicated tests, not just review.

## Authentication & Session

- Passwords hashed via ASP.NET Core Identity (PBKDF2 by default), never logged, never returned in any API response, never stored in `AuditLog` metadata.
- JWT access tokens are short-lived; refresh tokens rotate on use with reuse detection (family revocation on replay). Full flow: [architecture/authentication-flow.md](../architecture/authentication-flow.md).
- Refresh tokens are delivered via httpOnly, secure, `SameSite` cookies — not accessible to client-side JavaScript, reducing XSS exfiltration risk.

## Authorization

- Permission-based policies (`inventory.adjust`, `sales.refund`, ...), evaluated server-side on every protected endpoint — never inferred from what the client UI happens to show or hide.
- `TenantContext` is resolved exclusively from the validated JWT's `store_id` claim. No code path accepts a tenant identifier from a client-supplied header, query string, or request body field. This is the single most important line of defense against cross-tenant access and is called out explicitly in [ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md).

## Data Access

- All database access goes through EF Core with parameterized queries; no string-concatenated SQL anywhere, including raw-SQL/report code paths (if raw SQL is used for a reporting query, it uses parameters, never interpolation).
- EF Core global query filters apply `StoreId` scoping automatically as defense-in-depth on top of explicit predicates in the Application layer.

## Cross-Tenant Isolation Testing (the load-bearing control)

Dedicated integration tests, treated as a security test class:
- Authenticate as Tenant A; attempt to `GET`/`PUT`/`DELETE` a Tenant B entity by ID → must return 404, not the data, not a 403 (which would confirm existence).
- Authenticate as Tenant A; attempt a RAG query while Tenant B has uploaded a document containing distinctive content → the answer must never surface Tenant B's content.
- Attempt to forge/inject a different `store_id` via a request header or body field → must have no effect; only the signed JWT claim is honored.

See [testing/testing-strategy.md](../testing/testing-strategy.md) for how these are structured.

## Transport & Network

- HTTPS enforced in all non-local environments (HSTS in production).
- CORS allow-list restricted to known client origins per environment — no wildcard origin in any environment that carries credentials.

## Abuse Protection

- Rate limiting on `/auth/login`, `/auth/refresh`, `/auth/register` to blunt credential stuffing/brute force.
- Generic error messages on login failure (do not reveal whether the email exists).

## Secrets Management

- No secret (connection string, JWT signing key, Gemini API key) is ever committed to source control.
- Local development: `.env` (gitignored) / .NET user-secrets.
- Cloud: Azure Key Vault, accessed via Managed Identity — no secret stored as plain App Service configuration.
- `.env.example` documents every required variable name with a placeholder, never a real value.

## File/Document Handling

- Upload content-type allow-list and size cap enforced server-side (not just client-side validation).
- Files stored via the `IStorageService` abstraction; access requires the same tenant + permission checks as any other resource — a document URL is never a bare, unauthenticated public link.

## OWASP Top 10 — Applied Mapping

| OWASP Category | This System's Mitigation |
|---|---|
| Broken Access Control | Permission-based authorization + server-resolved TenantContext + dedicated cross-tenant tests |
| Cryptographic Failures | Identity password hashing, HTTPS everywhere, secrets never at rest in plaintext outside Key Vault |
| Injection | EF Core parameterization everywhere, no string-concatenated SQL |
| Insecure Design | Multi-tenancy and concurrency modeled at the schema/auth layer from Phase 1, not bolted on |
| Security Misconfiguration | Environment-specific config, no debug/detailed errors in non-local environments |
| Vulnerable Components | Dependency versions tracked, Dependabot/CI dependency scan (documented as a CI step) |
| Identification & Auth Failures | Rotating refresh tokens with reuse detection, rate limiting on auth endpoints |
| Software & Data Integrity | CI runs tests before any deploy; signed/verified container images (documented target) |
| Logging & Monitoring Failures | Correlation-ID-enriched structured logs, Application Insights in cloud, no secrets ever logged |
| SSRF | Not directly applicable (no user-supplied URL fetched server-side) in core scope; noted if a future feature introduces one |

## AI-Specific Security

See [architecture/ai-architecture.md](../architecture/ai-architecture.md) Guardrails section — fixed tool allow-list, per-call authorization re-check, no raw-SQL tool, prompt-injection-aware framing, tested with adversarial document content.

## Interview Questions This Creates

- "What's the single most important line of code protecting tenant isolation in this system?"
- "How do you know your authorization checks actually work, versus just believing they do?"
- "Where are secrets stored in each environment, and how does the app retrieve them?"
