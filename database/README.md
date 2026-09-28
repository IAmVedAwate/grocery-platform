# database

**This folder is intentionally empty apart from this file.**

EF Core migrations in `backend/src/Infrastructure/Persistence/Migrations/` are
the single source of truth for the schema, and they are applied automatically
on startup in Development and by the integration test suite. There are no
stored procedures, views, functions or triggers in this system — all data
access goes through EF Core and LINQ.

An earlier version of this README described seed scripts and a synthetic
performance dataset as if they lived here. They never did:

- **Seed data** — there is no seed script. `docs/operations/local-development.md`
  covers registering a store through the API instead.
- **The Phase 3 performance dataset** — the synthetic rows used for the SQL
  performance pass were generated ad hoc and deleted afterwards. The method,
  the queries, the `STATISTICS IO/TIME` output and both execution plans are
  reproduced in full in
  [docs/architecture/sql-performance-pass.md](../docs/architecture/sql-performance-pass.md),
  which is what actually documents that work.

Table-level schema design and indexing rationale live in
[docs/architecture/data-architecture.md](../docs/architecture/data-architecture.md).
