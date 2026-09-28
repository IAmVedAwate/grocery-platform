# Testing Strategy

Testing is a first-class project concern, not an afterthought — it is one of the developer's biggest stated skill gaps and is treated accordingly. Tests are written alongside each vertical slice, not retrofitted at the end of a phase.

## Test Pyramid

```
        ┌─────────────────────┐
        │   AI Evaluation      │   small, targeted — tool selection, RAG
        │   (Phase 5)          │   retrieval/citation, adversarial prompts
        ├─────────────────────┤
        │  Integration / API   │   WebApplicationFactory + Testcontainers
        │  (every phase)       │   SQL Server — real DB, real auth, real
        │                      │   HTTP pipeline
        ├─────────────────────┤
        │  Unit                │   xUnit (+ Moq) — domain rules,
        │  (every phase)       │   calculations, validation, no I/O
        └─────────────────────┘
        Architecture tests run alongside all of the above, asserting the
        dependency-direction rule in backend-architecture.md.
```

## Unit Tests

**Framework:** xUnit, with xUnit's own `Assert` API — **not** FluentAssertions, which is deliberately not referenced. An earlier draft of this document named it before any test existed; the assertions were written plain and never changed, so the dependency was never added. Recorded here rather than quietly corrected, because a testing doc that names a library the test projects don't reference is exactly the kind of claim someone checks.

Moq is used for `Application`-layer interfaces where genuinely needed (see `ProductApplicationServiceTests`, which asserts call *ordering* around storage) — never to mock the database in a test that's actually testing database behavior.

**What's covered:** domain business rules (stock cannot go negative, refund cannot exceed original quantity, purchase approval threshold logic), pricing/tax/discount calculations, DTO validation rules. These run with no database, no HTTP, in milliseconds.

## Integration Tests

**Framework:** `WebApplicationFactory<Program>` + **Testcontainers** running a real SQL Server container — explicitly **not** an in-memory database or a mocked repository. This is a direct, deliberate response to a documented past mistake: mocking a database in tests once let a broken behavior pass review that only showed up against the real engine's actual semantics. Integration tests in this project always run against the real engine.

**What's covered per phase:**
- **Phase 1:** registration → login → refresh → protected-endpoint access; permission-denied path for an under-permissioned user; cross-tenant access attempts (read/write another tenant's product by ID) asserted to fail.
- **Phase 2:** the full checkout transaction (stock check → payment → invoice → inventory update → audit, all-or-nothing); the concurrency scenario — two simultaneous requests racing to sell the last unit of stock, asserting exactly one succeeds and final stock is never negative; the full purchase-order → receive → inventory-increase flow, including partial receipt.
- **Phase 3:** report endpoints return correct, consistently-filtered results (the count matches the page contents' filter, per [api-conventions.md](../api/api-conventions.md)); rate limiting actually triggers on repeated auth failures.
- **Phase 4:** configuration/secrets resolution works per environment (smoke-tested against the deployed demo, not just locally).
- **Phase 5:** tool-calling executes against real backend data respecting the caller's real permissions; RAG retrieval respects tenant boundaries even when another tenant's document contains highly similar content.

## Architecture Tests

A small test project (or a targeted assertion set) enforcing the dependency-direction rule in [backend-architecture.md](../architecture/backend-architecture.md): `Domain` must not reference EF Core/ASP.NET Core/external SDKs; `Api` must not contain business logic bypassing `Application`. Fails the build if violated — this is what makes "Clean Architecture" here a testable property, not just a folder convention.

## AI Evaluation (Phase 5)

A fixed, small evaluation set of representative prompts, run against the assistant, checking:
- **Tool selection correctness:** does "which products are below reorder level" actually invoke `get_low_stock_items` rather than answering from the model's general knowledge?
- **Permission respect:** does an under-permissioned user's request correctly fail at tool-execution time even if the model attempts the call?
- **RAG grounding:** does a document question return a citation, and does an out-of-scope question correctly produce "I don't know" rather than a hallucinated answer?
- **Adversarial resistance:** a document containing an embedded instruction ("ignore previous instructions...") does not cause the assistant to deviate from its guardrails.

This is not a full LLM-eval framework — it is a small, deliberately-curated regression set proportionate to the project's scope, expanded only if it earns its keep.

## Realistic Dataset Sizing (for performance-related tests)

Phase 3's SQL performance work runs against a seeded dataset large enough to make a table-scan-vs-index-seek difference observable (order of magnitude: tens of thousands of `StockMovement`/`SalesOrder` rows per test tenant, seeded via a script in `database/`), not a handful of demo rows where every query is fast regardless of indexing.

## CI Enforcement

Every pull request/push runs: unit tests → integration tests (Testcontainers spins up its own SQL Server in the CI runner) → architecture tests. A failing test blocks the build; there is no "tests are advisory" mode. See [operations/local-development.md](../operations/local-development.md) and the `.github/workflows/` pipeline.

## What Is Deliberately Not Done

Full end-to-end browser automation (e.g., Playwright driving the actual Next.js UI) is noted as a valuable future addition but is not required for the core 5 phases — API-level integration tests cover the business-critical paths, and manual verification (per the `run` workflow) covers the UI layer, given the time budget.
