# ADR-006: Background Processing Approach

**Status:** Accepted — Phase 3

## Problem

The platform needs at least one legitimate asynchronous background process (§25 of the PRD): generating low-stock notifications, and later, pre-computing reports. A mechanism must be chosen.

## Options Considered

1. **Message broker + worker** (RabbitMQ/Kafka + a separate consumer process).
2. **Scheduled job runner** (e.g., Hangfire/Quartz) with its own persistence.
3. **In-process `IHostedService` reading a durable outbox/job table** in the same database, on a timer.

## Decision

**Option 3: in-process `IHostedService` over a durable job table.**

## Reasoning

- The actual workload (checking for low-stock crossings, periodically pre-computing a report) is low-volume and does not need a message broker's delivery guarantees, ordering semantics, or horizontal consumer scaling. Introducing Kafka/RabbitMQ here would be exactly the pattern the project's own brief explicitly warns against: adding infrastructure for résumé value rather than a proven need.
- A durable table-backed job queue still teaches the real concept a broker would (asynchronous work decoupled from the request/response cycle, at-least-once processing, idempotent job handlers) without a second piece of infrastructure to run, secure, and explain.
- `IHostedService` runs in the same deployable as the API in this modular-monolith architecture (ADR-001), consistent with the project's "boring/simple until complexity is justified" principle.

## Trade-offs

- **Given up:** independent scaling of background work, delivery guarantees and replay semantics a real broker provides, resilience if the API process itself is down (the worker is down with it).
- **Gained:** one deployable, one thing to operate, and a background-processing story that's honestly proportionate to the actual workload.

## Consequences

- Job handlers are written to be idempotent (safe to reprocess) since the durable-table approach is at-least-once, not exactly-once.
- If a real need for independent scaling or stronger delivery guarantees emerges later (e.g., report generation becomes heavy enough to need its own compute), that becomes a future ADR with a measured justification — not a default upgrade.

## Interview Questions This Creates

- "Why not a message queue for background work?"
- "What would make you introduce one?"
- "How do you handle a job that fails halfway through?"
