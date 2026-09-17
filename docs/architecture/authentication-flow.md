# Authentication & Authorization Flow

See [ADR-004](../decisions/ADR-004-authentication-architecture.md) for why this design was chosen. This document describes the mechanics.

## Login

```
1. Client POSTs { email, password } to /api/v1/auth/login
2. ASP.NET Core Identity verifies the password hash for the user matching
   that email WITHIN the tenant resolved from the request (store subdomain
   or explicit store selector at login — exact UX decided in Phase 1
   implementation)
3. If valid and user.IsActive:
   a. Issue a JWT access token (short-lived, e.g. 15 min) containing:
      sub (user id), store_id (tenant), permissions (resolved claim set),
      exp
   b. Issue a refresh token: generate a random value, store its HASH plus
      a FamilyId in the RefreshToken table, return the raw value to the
      client (never store the raw value)
4. Client stores the access token in memory and the refresh token in an
   httpOnly, secure cookie (not localStorage, to reduce XSS exposure)
```

## Token Refresh

```
1. Client POSTs the refresh token (via the httpOnly cookie) to
   /api/v1/auth/refresh
2. Server hashes the presented token and looks it up:
   - Not found → reject
   - Found but RevokedAtUtc set → reject AND revoke the entire token
     FAMILY (reuse detection: this token was already rotated away, so its
     reappearance means it was likely stolen and someone is replaying it)
   - Found, valid, not expired → issue a NEW access token + a NEW refresh
     token in the same family; mark the presented token as
     Revoked/ReplacedByTokenId
3. Deactivated users (IsActive = false) are rejected here even if their
   refresh token is technically still valid/unexpired — active status is
   checked on every refresh, not just at login
```

## Authorization

```
1. Every protected endpoint declares a required permission, e.g.
   [RequirePermission("inventory.adjust")]
2. A custom IAuthorizationHandler reads the permissions claim from the
   validated JWT and checks membership — no role-string comparison
   anywhere in this path
3. TenantContext (an injected, request-scoped service) reads store_id
   from the validated JWT claims — NEVER from a client-supplied header,
   query parameter, or body field. This is the actual enforcement anchor
   for multi-tenancy (ADR-002): every EF Core query filter reads from
   this TenantContext, and TenantContext itself only trusts the token
```

## Why the Tenant Boundary Can't Be Spoofed by a Client

A malicious client cannot claim to be a different store by sending a different `X-Store-Id` header or similar, because no code path reads the tenant from anything other than the cryptographically-signed JWT claims validated by ASP.NET Core's authentication middleware before the request reaches application code. This is the specific answer to "how do you actually stop tenant A from reading tenant B's data" — see [ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md).

## Password Reset / Account Lifecycle (Phase 1/1.x)

Standard Identity-backed reset-token flow (time-limited, single-use token emailed or displayed in dev). Deactivation flips `User.IsActive` and is checked at both login and refresh — see above.

## Interview Questions This Creates

- "Walk me through what happens, end to end, from a login POST to a protected API call succeeding."
- "How is a stolen refresh token detected and contained?"
- "Where exactly does the multi-tenant boundary get enforced, and why can't a client bypass it?"
