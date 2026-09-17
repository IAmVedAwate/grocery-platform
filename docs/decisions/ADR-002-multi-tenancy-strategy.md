# ADR-002: Multi-Tenancy Strategy

**Status:** Accepted — Phase 1 (locked before implementation begins)

## Problem

The platform's core product bet is serving many independent grocery stores (tenants) from one system, eventually enabling aggregated insight across them. Each store's data must be completely isolated from every other store's — a leak here is not a bug, it's a trust-destroying failure for both the product concept and the portfolio credibility of the project. The strategy must be decided *before* the schema is written, because retrofitting tenancy onto a single-tenant schema is a much larger, riskier change than building it in from the start.

## Options Considered

1. **Database-per-tenant** — a separate SQL Server database per store.
2. **Schema-per-tenant** — one database, one schema per store.
3. **Shared database, shared schema, row-level isolation** — one database, one schema, every tenant-owned table carries a `StoreId` column; isolation enforced at the query layer.

## Decision

**Option 3: shared database, shared schema, row-level isolation**, enforced through two layers: (a) EF Core global query filters keyed on a server-resolved `TenantContext`, and (b) explicit `StoreId` predicates as the primary, audited enforcement point — the query filter is defense-in-depth, not the only mechanism relied upon.

## Reasoning

- **Database-per-tenant** doesn't scale operationally for a solo developer (migrations must run N times, connection management multiplies, cost scales linearly with tenant count) and is overkill for the isolation guarantee actually needed here — it's the right answer for tenants needing separate compliance/backup regimes, which this product does not have.
- **Schema-per-tenant** sits awkwardly in between: still multiplies migration/connection complexity, without database-per-tenant's stronger isolation guarantee.
- **Shared schema** is the standard, proven approach for early-stage multi-tenant SaaS (and is what makes the stated long-term goal — aggregating anonymized cross-tenant data — practical later: the data already lives in one place with consistent structure).
- The isolation risk of shared schema is real but manageable with discipline: `TenantContext` is resolved *server-side* from the validated JWT's `store_id` claim (never from a client-supplied header/parameter, which would let a client simply claim to be a different tenant), every tenant-owned table gets an EF Core global query filter, and every write path validates the entity being mutated actually belongs to the current tenant before mutating it.

## Trade-offs

- **Given up:** the strongest possible isolation guarantee (physical separation); a compromised query filter bug has a wider blast radius than in database-per-tenant.
- **Gained:** one schema to migrate and evolve, trivial to add a tenant (a row, not a deployment), and a data layout that supports the platform's stated long-term data-network ambition without a later migration.

## Consequences

- Every new table added to the system *must* be reviewed for whether it's tenant-scoped or global — this is a checklist item, not an afterthought.
- Integration tests are required that specifically attempt cross-tenant access (read another tenant's entity by ID, attempt to mutate it) and assert failure — this is treated as a security test class, not a functional test nice-to-have (see [security/security-model.md](../security/security-model.md) and [testing/testing-strategy.md](../testing/testing-strategy.md)).
- Composite indexes across the schema lead with `StoreId` for the tenant-scoped access pattern that dominates nearly every query.

## Interview Questions This Creates

- "How do you actually stop tenant A from seeing tenant B's data — where's the enforcement point?"
- "Why not a separate database per tenant?"
- "What's a global query filter, and why isn't it the *only* thing you rely on?"
- "How would you evolve this if one enterprise customer later demanded physical data separation?"
