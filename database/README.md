# database

SQL scripts outside of EF Core migrations: seed data for local development (a demo tenant with representative products/suppliers/customers), the larger synthetic dataset used for Phase 3 SQL performance work, and any ad-hoc query notes captured during index/execution-plan investigations.

Table-level schema design lives in [docs/architecture/data-architecture.md](../docs/architecture/data-architecture.md) — EF Core migrations (in `backend/src/Infrastructure`) are the source of truth for the actual schema.
