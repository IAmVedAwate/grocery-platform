# ADR-001: Modular Monolith vs. Microservices

**Status:** Accepted — Phase 1

## Problem

The platform needs a service architecture. The reference target-skill resume lists "microservices," and it's tempting to default to it for résumé value. The actual constraints are: one developer, ~4 focused hours/day, a system that must be demoable and coherent within a portfolio timeline, and a business domain (Catalog, Inventory, Purchasing, Sales, Reporting, AI) with heavy transactional coupling between modules (a sale touches inventory, invoicing, and audit in one atomic operation).

## Options Considered

1. **Full microservices** — one deployable service per module (Catalog service, Inventory service, Sales service, etc.), communicating over HTTP/messaging.
2. **Modular monolith** — a single deployable, internally organized into modules with enforced boundaries (`Domain`/`Application` per module, shared `Infrastructure`), sharing one database.
3. **Modular monolith + selective service extraction** — start as (2), extract a service later only if a specific module has genuinely independent scaling, deployment, or team-ownership needs.

## Decision

**Modular monolith (option 2), with the architecture kept clean enough that option 3 remains possible later** if a real reason emerges (it is not expected to during the 5-phase core scope).

## Reasoning

- The checkout workflow (§22 of the PRD) needs atomic consistency across sale, inventory, invoice, and audit. In a microservices split, this becomes a distributed transaction/saga problem — solving that well is itself a multi-week undertaking, and solving it *badly* (dual writes without compensation) is a worse engineering demonstration than not attempting it.
- One developer cannot realistically operate N independently deployed services (N sets of CI/CD, N sets of observability, N network boundaries to secure) inside a 4-hour/day budget without the operational overhead crowding out the business-logic and learning work the project actually exists for.
- A modular monolith still teaches the *design* skill that matters most for a "why microservices" interview question — module boundaries, dependency direction, and interface contracts — without the incidental complexity of a distributed system that doesn't need to be distributed yet.

## Trade-offs

- **Given up:** independent scaling of hot modules (e.g., scaling Sales separately from Reporting), independent deployability, polyglot flexibility, fault isolation between modules.
- **Gained:** transactional simplicity, one CI/CD pipeline, one thing to secure/observe/deploy, much lower operational cost for a solo developer, faster iteration speed.

## Consequences

- Module boundaries must still be enforced in code (namespace/project structure, an architecture test asserting no illegal cross-module references) so the system doesn't degrade into an unstructured "big ball of mud" monolith — the discipline of microservices without the distribution cost.
- If a real scaling/team need for extraction appears later, the module boundaries already in place make extraction a refactor, not a rewrite.

## Interview Questions This Creates

- "Why didn't you use microservices?"
- "What would make you split this into services later?"
- "How do you keep a monolith from becoming an unmaintainable mess?"
- "How does your checkout transaction stay consistent, and what would change if Inventory were a separate service?"
