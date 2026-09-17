# ADR-007: Caching Strategy

**Status:** Accepted — Phase 3

## Problem

Some reads (product catalog, category/brand lists, configuration) are requested far more often than they change. The system needs a caching approach that improves read latency without introducing staleness or consistency risk into data that actually changes frequently (inventory, sales).

## Options Considered

1. **Redis** — distributed cache, shared across instances, supports pub/sub invalidation.
2. **`IMemoryCache`** — in-process cache, per-instance, no additional infrastructure.
3. **No caching** — rely entirely on indexed database reads.

## Decision

**`IMemoryCache` for catalog/category/brand reads, with explicit TTL and write-triggered invalidation. No Redis in the core phases.**

## Reasoning

- The candidate data (catalog/category/brand lists) is read-heavy, low-cardinality-of-change, and tolerates brief staleness — a textbook `IMemoryCache` case.
- The system runs as a single deployable in the core phases (ADR-001); a distributed cache's main advantage — consistency across multiple instances — has no payoff yet. Adding Redis now would again be infrastructure introduced ahead of a proven need, the exact anti-pattern the project's brief warns against ("using Redis everywhere without a reason").
- Skipping caching entirely was rejected because the catalog read path is genuinely hot (every product search/lookup, every checkout screen load) and demonstrating *deliberate, justified* caching — with a stated TTL and invalidation trigger — is a stronger performance-engineering story than either extreme.

## Trade-offs

- **Given up:** cache consistency across multiple API instances (each instance has its own in-memory cache, so a write on instance A doesn't invalidate instance B's cached copy until that entry's TTL expires) — acceptable because the system runs as one instance in the core deployment; documented as a real limitation if horizontal scaling is added later.
- **Gained:** zero additional infrastructure, trivial invalidation logic, immediate latency improvement on the hottest read paths.

## Consequences

- Every cached entry has an explicit TTL and an explicit invalidation trigger (write to the underlying entity clears the relevant cache key) — no cache entry exists without both being documented at the call site.
- If/when the API scales to multiple instances, this ADR is revisited and Redis (or a cache-invalidation broadcast mechanism) becomes the next decision, made for a measured reason rather than by default.

## Interview Questions This Creates

- "What do you cache and why specifically those reads?"
- "How do you invalidate the cache when the underlying data changes?"
- "What breaks about this approach if you scale to multiple instances, and how would you fix it?"
