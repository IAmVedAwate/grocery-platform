# Shared

Cross-cutting concerns used across modules: Problem Details error model, correlation ID plumbing, the audit-log writer, permission constants. Referenced by `Application`, `Infrastructure`, and `Api` — never contains module-specific business logic.

Not yet scaffolded — first code lands in Phase 1 (Slice 0) per [docs/ROADMAP.md](../../../docs/ROADMAP.md).
