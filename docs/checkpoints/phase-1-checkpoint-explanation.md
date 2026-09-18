# Phase 1 Checkpoint — Foundation, Identity & Multi-Tenant Core

This explains what Phase 1 actually built, what you need to understand about it, and how to talk about it in an interview — with exact file/line references so every claim below is something you can pull up and point at, not something to take on faith. See [ROADMAP.md](../ROADMAP.md#phase-1--foundation-identity--multi-tenant-core-p0) for the original checkpoint definition this expands on.

---

## 1. Engineering Checkpoint — what was actually built

**Tenant/auth foundation:**
- `Store` is the tenant root — `backend/src/Domain/Store.cs:17` (constructor), `:31` (`Suspend()`).
- JWT access tokens, signed and issued with `sub`, `store_id`, and a `permission` claim per permission the user's roles grant — `backend/src/Infrastructure/Identity/JwtTokenService.cs:12` (`GenerateAccessToken`), `:29` (key construction), `:32` (token construction).
- Rotating refresh tokens with reuse detection — `backend/src/Infrastructure/Identity/RefreshTokenServiceImpl.cs:18` (`IssueAsync`), `:24` (`RotateAsync`), `:31` (the reuse-detection branch: a presented token that's already been revoked triggers `:35` `RevokeFamilyAsync`, killing the whole token family, not just the one token).
- Tenant resolution **exclusively** from the validated JWT's claims, never from anything client-supplied — `backend/src/Infrastructure/Identity/HttpTenantContext.cs:19-20` (`StoreId`), `:23-24` (`UserId`), `:27` (`GetRequiredClaim`).
- Permission-based authorization via a dynamic policy provider (no role-string checks anywhere) — `backend/src/Api/Authorization/PermissionRequirement.cs:9`, `PermissionAuthorizationHandler.cs:9` (the actual claim check), `PermissionPolicyProvider.cs:13,22,25` (builds a policy for any `permission:<key>` name on demand), `RequirePermissionAttribute.cs:16` (wires an attribute to that policy). Used on every protected endpoint, e.g. `backend/src/Api/Controllers/ProductsController.cs:16,33,41,52,63`.

**Multi-tenancy at the data layer:**
- EF Core global query filters scope every tenant-owned table to the current request's `Store` — `backend/src/Infrastructure/Persistence/GroceryDbContext.cs:86` (`Product`), `:92,99,106` (`Category`/`Brand`/`Unit`), `:167` (`AuditLogEntry`), `:176,187` (Inventory), `:197,208` (Purchasing), `:223,241` (Sales). The long comment starting at `:69` documents *why* these are written the specific way they are — see §3 below, it's the single most important thing to understand in this whole phase.
- ASP.NET Core Identity's defaults assume one global tenant and had to be overridden at three separate layers — validators (`TenantScopedRoleValidator.cs:14,22`, `TenantScopedUserValidator.cs:13`), the database schema (composite unique indexes in `GroceryDbContext.cs`), and the user-store's role-assignment method itself (`IdentityServiceImpl.cs:51-59`, using `db.UserRoles.AddAsync` directly instead of `userManager.AddToRoleAsync`).

**Catalog module (the first vertical slice):**
- `Product` domain entity with validation baked into its constructor/mutators so an invalid product can't exist in memory — `backend/src/Domain/Catalog/Product.cs:29` (constructor), `:106,112` (price/tax validators).
- Registration flow ties a `Store` + its default role bundle + an admin `User` together in one use case — `backend/src/Application/Identity/AuthApplicationService.cs:21` (`RegisterStoreAsync`), `:41-42` (creating every default role for the new store).
- Pagination where the count query uses the *exact same filter* as the page query — `backend/src/Infrastructure/Persistence/ProductRepository.cs:32-36`.

**Web client:** login/register/products pages, an in-memory access token (never localStorage), and a silent-refresh-on-mount so a page reload doesn't force a re-login — `web/src/lib/auth-context.tsx:48-51` (the mount-time refresh attempt), `web/src/lib/api-client.ts:4` (API base URL, with the port-7223-vs-5292 lesson baked into the comment at `:2`).

**Deployment:** multi-stage Dockerfile, non-root `$APP_UID`, Docker Compose wiring API + SQL Server, health checks split into a dependency-free `/health/live` and a DB-checking `/health/ready` — `backend/src/Api/Program.cs:167-168`.

---

## 2. Learning Checkpoint — what you need to actually understand

- **JWT structure and validation.** A JWT is a signed (not encrypted) claim set — anyone can *read* it, only the holder of the signing key can *produce a valid one*. Your access token carries `sub`, `store_id`, and a `permission` array; the API validates the signature, issuer, audience, and expiry on every request via `AddJwtBearer` (`Program.cs:89-90`).
- **Why refresh rotation matters.** A single long-lived refresh token is a single point of compromise. Rotation means every use invalidates the previous token; reuse of an already-rotated token is treated as theft and kills the whole session family, not just that token.
- **EF Core global query filters, and the specific gotcha that bit you.** A filter written inside a *separate* configuration class closes over *that class's* field, not the DbContext's — and because `OnModelCreating` runs once per DbContext type (the compiled model is cached), that stale reference sticks around for the life of the process. The fix is that every filter must reference a member of `this` (the DbContext), which is what the giant comment at `GroceryDbContext.cs:69` explains in full.
- **Dependency-direction enforcement is a testable property, not a folder convention.** `backend/tests/Architecture/DependencyDirectionTests.cs` actually fails the build if `Domain` ever references EF Core or ASP.NET Core.
- **Why ASP.NET Core Identity's defaults broke under multi-tenancy.** Identity was built assuming a single tenant. Role names, usernames, and even the internal `UserStore.IsInRoleAsync` all assume global uniqueness by name. You didn't patch around this blindly — you found each of the three layers independently, understood *why* each one broke, and fixed each at the right layer (see `docs/operations/troubleshooting.md` for the full narrative).

---

## 3. Interview Checkpoint — questions and how to answer them

> Answers are written in first person, the way you'd actually say them. Each one names the file/line to have open or mention if asked to go deeper.

**Q: Walk me through what happens, end to end, from a login POST to a protected API call succeeding.**

> "The client posts store slug, email, and password to `/api/v1/auth/login`. `AuthApplicationService.LoginAsync` looks up the store by slug, validates credentials through `IIdentityService` — which under the hood uses ASP.NET Core Identity's password hasher, not anything I wrote myself — and if that succeeds, resolves the user's permissions and calls `JwtTokenService.GenerateAccessToken` (`JwtTokenService.cs:12`), which puts `sub`, `store_id`, and one `permission` claim per granted permission into a signed JWT. A refresh token is issued alongside it and set as an httpOnly cookie. On the next request, the client sends the access token as a Bearer header; ASP.NET Core's JWT middleware validates the signature and expiry, and then my `PermissionAuthorizationHandler` (`PermissionAuthorizationHandler.cs:9`) checks whether the token's `permission` claims include whatever the endpoint requires via `[RequirePermission(...)]`."

**Q: Access token vs refresh token — why both?**

> "The access token is short-lived and sent on every request, so if it leaks, the exposure window is small. The refresh token is longer-lived but only ever sent to one endpoint, `/auth/refresh`, over an httpOnly cookie the client-side JavaScript can't even read. Splitting them means I get short-lived exposure without forcing the user to log in every few minutes."

**Q: How do you detect and respond to refresh token theft?**

> "Every refresh token belongs to a family, tracked by `FamilyId`. Using a token rotates it — the old one is marked revoked and a new one issued in the same family (`RefreshTokenServiceImpl.cs:42`). If a token that's *already been revoked* gets presented again, that's a signal someone replayed a captured token, so instead of just rejecting it, I revoke the entire family (`RefreshTokenServiceImpl.cs:31-35`) — every token descended from that original login stops working, forcing a fresh login."

**Q: How does your system stop tenant A from reading tenant B's data — what's the actual enforcement point?**

> "Two layers, deliberately redundant. First, the tenant ID is never trusted from anything the client sends — it's read exclusively from the validated JWT's `store_id` claim, in `HttpTenantContext.cs:19-20`. There's no header or query parameter anywhere in the codebase that can override it. Second, every tenant-owned table has an EF Core global query filter — `Product`, for example, at `GroceryDbContext.cs:86` — that adds `WHERE StoreId = @currentTenant` to *every* query automatically, so even if application code forgot to filter explicitly, the ORM does it for you. I don't just assert this works — `TenantIsolationTests.cs` proves it: one test logs in as store A, creates a product, then logs in as store B and asserts the product doesn't appear in the list (`TenantIsolationTests.cs:21`) and a direct `GET` by ID returns 404, not 403 (`TenantIsolationTests.cs:37`) — 403 would leak that the resource exists at all."

**Q: What's the single most important line of code protecting tenant isolation in this system?**

> "It's not one line, it's a pattern I had to learn the hard way: every `HasQueryFilter` call in `GroceryDbContext.cs` has to reference `tenantContext` as a member of `this` — the DbContext instance — not a copy held by some other object. I originally wrote the Product filter inside a separate `ProductConfiguration` class that took its own `ITenantContext` — and because EF Core only compiles the model once per process and only rebinds `this.<field>`-style accesses per DbContext instance, that filter permanently used whichever tenant happened to make the *first* request after the app started. Writes were fine because application code reads the tenant context directly; only *reads* through the cached filter were stale. It passed a single-store smoke test and only broke once a second store existed. I've documented the whole thing in `docs/operations/troubleshooting.md` because it's exactly the kind of bug that looks like it works until it very much doesn't."

**Q: Why permission-based authorization instead of role checks?**

> "Role checks like `if (role == "Manager")` don't scale past a handful of roles and don't express fine-grained rules — a Cashier needing `sales.create` but not `sales.refund` can't be expressed cleanly with role comparisons. Instead, every JWT carries a `permission` claim per granted permission, resolved once at login from whatever roles the user has. `PermissionPolicyProvider.cs:22-25` builds an authorization policy dynamically for any `permission:<key>` name, so I don't have to pre-register ten policies by hand — and `[RequirePermission(Permissions.CatalogManage)]` on a controller action reads exactly like what it checks."

**Q: Why ASP.NET Core Identity instead of hand-rolling password hashing?**

> "Password hashing is a solved, security-critical problem — get the algorithm, salt, or iteration count wrong and you've built something the industry has already learned the hard way not to do. Identity's default hasher (PBKDF2) is the professional default, so I used it for credential storage instead of reinventing it, and spent my actual engineering effort on the parts that were genuinely novel for this system: the tenant model, refresh rotation, and permission-based authorization, none of which Identity provides out of the box."

**Q: Tell me about a hard bug you ran into.**

> "Multi-tenancy, three separate times in the same session. ASP.NET Core Identity assumes role names and usernames are unique *globally* — it's built for single-tenant apps. First, the default `IRoleValidator` rejected a second store's 'Admin' role as a duplicate. I fixed that with a tenant-scoped validator (`TenantScopedRoleValidator.cs:22`). Then the exact same assumption turned out to be baked into the database schema too — a unique index on `NormalizedName` — so I had to override that in `GroceryDbContext.cs` and add a composite `(StoreId, NormalizedName)` index instead. Then — and this one surprised me — even after both of those fixes, `UserManager.AddToRoleAsync` *itself* does a global lookup by role name internally, so assigning a role to a user still threw once two stores both had an 'Admin' role. I ended up bypassing that specific method and inserting the `AspNetUserRoles` join row directly by `RoleId`, since role IDs actually are globally unique (`IdentityServiceImpl.cs:51-59`). Each fix looked complete until the next layer surfaced. I wrote the whole thing up in `docs/operations/troubleshooting.md` because it's a genuinely useful lesson: a library's defaults can embed an architectural assumption — single tenant — that you only discover by actually testing with a second tenant, which is exactly why `TenantIsolationTests.cs` exists."

**Q: How do you avoid loading an entire table into memory for a list endpoint?**

> "Every list endpoint takes `page`/`pageSize` and applies `Skip`/`Take` at the database level, not in memory — `ProductRepository.cs:36-41`. And specifically, the total count uses the *identical* filter predicate as the page query (`ProductRepository.cs:32-36`) — I called that out deliberately because in earlier SQL practice I'd seen a count query silently drift out of sync with the paged query's filters, producing a 'page 3 of 5' that doesn't actually match what's returned. It's a small discipline but it's the kind of bug that's invisible until someone notices the numbers don't add up."

**Q: What's your Docker setup, and why non-root / multi-stage?**

> "Multi-stage: the SDK image does the build and publish, but the final runtime image only contains the published output on the much smaller ASP.NET runtime image — no compiler, no build tooling, smaller attack surface and smaller image. For the user, I didn't hand-roll a `useradd` — Microsoft's ASP.NET runtime images ship a built-in unprivileged `app` user specifically for this, so the container runs as `$APP_UID`, not root."

---

## 4. Evidence Checkpoint — how to actually show this works

```
cd backend
dotnet test tests/Architecture   # dependency-direction rule enforced, not just documented
dotnet test tests/Unit           # domain validation rules (Product, PageRequest, PermissionAuthorizationHandler)
dotnet test tests/Integration    # real SQL Server via Testcontainers — auth flow, tenant isolation, product CRUD
```

- `TenantIsolationTests.cs:21,37,55` — the three tests that specifically try to break the tenant boundary and prove they can't.
- `AuthFlowTests.cs:63` (`Refresh_RotatesToken_AndOldCookieNoLongerWorks`) — proves rotation actually happens, not just that the endpoint returns 200.
- `docs/operations/troubleshooting.md` — the three real bugs found and fixed in this phase, with root cause and fix, not a sanitized retelling.
- A live demo: register a store, add a product, log out, reload the page, and it's still logged in (silent refresh) — or open a second store and show the first store's product doesn't appear anywhere.
