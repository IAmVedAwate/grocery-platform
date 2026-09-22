# ADR-004: Authentication & Session Architecture

**Status:** Accepted — Phase 1

## Problem

The platform needs secure authentication for multi-tenant users, a session model that works for a stateless, horizontally-scalable API, and an authorization model expressive enough to support permission-based access control (not just coarse roles), per §13–14 of the PRD.

## Options Considered

**Credential storage:**
1. Hand-rolled password hashing.
2. ASP.NET Core Identity's user store and default password hasher.

**Session/token model:**
1. Server-side session (cookie + server session store).
2. JWT access token only, long-lived, no refresh.
3. JWT short-lived access token + rotating, persisted refresh token with reuse detection.

**Authorization model:**
1. Role-string checks (`if (role == "Manager")`).
2. Claims-based permission policies (`inventory.adjust`, `sales.refund`, ...) evaluated by custom `IAuthorizationHandler`s.

## Decision

- **ASP.NET Core Identity** for credential storage and password hashing.
- **JWT short-lived access token + rotating refresh token with reuse detection.**
- **Claims-based, permission-string authorization policies.** As of the staff-management feature, permissions are assigned directly per user (`UserPermissionEntity`), editable individually from Settings — not derived from role membership at read time. Roles (`ApplicationRole`/`RolePermissionEntity`) still exist and are used exactly once, as the starting bundle for the store-registration admin account (`IdentityServiceImpl.CreateUserAsync`); every staff account created afterward (`CreateStaffUserAsync`) has no role at all, only an explicit permission set. This is a deliberate step past "RBAC with extra steps" — see `docs/architecture/authentication-flow.md` for the mechanics and the reasoning for not modeling this as roles.

## Reasoning

- Password hashing is a solved, security-critical problem; hand-rolling it has no learning value proportional to its risk. Using Identity's hasher (PBKDF2 by default) is the professional default and is what "secure password hashing" means in §16 of the PRD.
- A stateless JWT access token supports horizontal scaling with no server-side session store — matches §17's "stateless API" non-functional requirement. Its short lifetime bounds the damage window if a token is captured.
- Refresh tokens are persisted (hashed at rest) and single-use: rotating a refresh token invalidates the previous one, and if an already-rotated token is presented again, the entire token family is revoked (reuse detection). This is the standard mitigation against refresh-token theft/replay and is explicitly documented so the developer can explain *why* rotation matters, not just that it exists.
- Role-string checks scattered through controllers don't scale past a handful of roles and don't map to the PRD's stated need for fine-grained operations like `inventory.adjust` vs `inventory.read`. A permission-string policy, resolved once at token-issue time into a claim set and evaluated via a small number of reusable authorization handlers, keeps authorization logic centralized and testable.

## Trade-offs

- **Given up:** the simplicity of a single long-lived token or server session; refresh rotation adds real implementation and testing surface (token family tracking, reuse detection).
- **Gained:** a session model that survives horizontal scaling, a bounded blast radius for a leaked access token, and an authorization model that can express real business rules instead of coarse role gates.

## Consequences

- `TenantContext` resolution (ADR-002) depends on the `store_id` claim issued into this JWT — the two decisions are coupled and must stay consistent.
- Revoking a compromised user's access (deactivation) must be checked against the refresh-token store, not just token expiry, or a deactivated user could keep refreshing indefinitely.

## Interview Questions This Creates

- "Access token vs refresh token — why both?"
- "How do you detect and respond to refresh token theft?"
- "Why permission-based authorization instead of role checks?"
- "How does a deactivated user actually get locked out, end to end?"
