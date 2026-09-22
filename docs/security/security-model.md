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

### Account enumeration on login — closed on all three channels

Every login failure — unknown store slug, unregistered email, wrong password, deactivated account — returns an **identical** `401` with the single message `Invalid credentials.`. The distinguishing reason is written to the server log, never to the response. This is enforced, not just intended:

| Channel | How it's equalized |
|---|---|
| **Status code** | All four modes throw `UnauthorizedAppException` → `401`. Never `404` — that both answers an existence question an unauthenticated caller hasn't earned and reads to a real user as "your account is gone" when they just mistyped a password. |
| **Response body** | One shared default message; the submitted email is never echoed back. `AuthFlowTests.Login_Failures_AreAll401_AndByteForByteIdentical` compares the full bodies rather than trusting they happen to match. |
| **Response time** | Password hashing (PBKDF2) is deliberately slow, so returning early on an unknown email/store would answer in ~12ms versus ~160ms and leak by timing alone. `IdentityServiceImpl.ValidateCredentialsAsync` verifies against a throwaway hash when no user matches, and `LoginAsync` routes an unknown store through the same check with a non-matching store id. Measured: ~0.16s across all three modes. |

An earlier version of this system got the first two wrong (404, `"User 'x' was not found."`) and the third wrong by omission — see [troubleshooting.md](../operations/troubleshooting.md).

**Refresh-token failures are also `401`, not `403`** — a token we can't validate is an authentication failure, not an authorization one — with one flat message (`"Your session has expired. Please sign in again."`) whether the token is unrecognized, already-rotated (reuse detected), expired, or belongs to a deleted/deactivated user. The client's correct reaction is the same in every case, so distinguishing them would only tell whoever holds a stolen token how far they got.

### Residual: `forgot-password` timing (measured, judged acceptable)

`/auth/forgot-password` already returns an identical `200` with no body detail whether the store, email, or neither exists. Its timing was measured rather than assumed: known email ~13–56ms, unknown email ~11–17ms, unknown store ~8ms. Unlike login, the spread is single-digit milliseconds and the ranges **overlap between runs**, because reset-token generation is HMAC-based and cheap — there's no PBKDF2-scale asymmetry to hide. Combined with rate limiting on the endpoint, this is not considered a practical oracle and has been left alone rather than padded with artificial work. Recorded here because "we measured it and decided" is a different claim from "we didn't look" — if an SMTP integration ever lands, the send path will add real asymmetry and this needs re-measuring.

### Where revealing existence *is* correct

Not every "already exists" message is a leak, and blanket-genericizing them would make the product worse:

- `"A user with email 'x' already exists for this store."` — requires `users.manage`; that caller can already list every user in the store.
- `"Barcode 'x' is already in use."` — requires `catalog.manage`, tenant-scoped to data the caller owns.
- `"Store slug 'x' is already taken."` — unauthenticated, but slug availability is inherent to any signup form, exactly like a username check.

The distinguishing question is whether the message tells an **unauthenticated or unauthorized** caller something about data they can't otherwise see.

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
