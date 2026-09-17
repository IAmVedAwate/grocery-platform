# API Conventions

## URL Structure & Versioning

- Resource-oriented, plural nouns: `/api/v1/products`, `/api/v1/sales-orders/{id}`.
- Version in the URL path from the first endpoint written — cheap to add now, expensive to retrofit once clients depend on unversioned URLs.
- Nested resources only where genuinely owned (`/api/v1/purchase-orders/{id}/items`), not for every relationship.

## HTTP Status Codes

| Code | Meaning in this API |
|---|---|
| 200 | Successful read or update returning a body |
| 201 | Successful creation; `Location` header set to the new resource |
| 204 | Successful action with no body (e.g., delete/deactivate) |
| 400 | Validation failure — malformed request, business-rule violation surfaced as client error |
| 401 | No valid authentication presented |
| 403 | Authenticated but lacking the required permission |
| 404 | Not found — **also** returned when a resource exists but belongs to another tenant (never 403 for cross-tenant access, which would leak existence) |
| 409 | Conflict — concurrency conflict (stale `RowVersion`), duplicate idempotency key, business-state conflict (e.g., approving an already-approved PO) |
| 500 | Unexpected server error — generic body to the client, full detail logged server-side with correlation ID |

## Error Response Shape (RFC 7807 Problem Details)

```json
{
  "type": "https://quickstock.dev/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Quantity must be greater than zero.",
  "traceId": "00-4bf9...-01",
  "errors": {
    "quantity": ["Quantity must be greater than zero."]
  }
}
```

Every error response includes `traceId`, matching the request's correlation ID, so a user-reported error can be found directly in logs.

## Pagination, Filtering, Sorting, Search

- List endpoints accept `page`, `pageSize` (offset pagination for admin/report screens where "page 4 of 20" matters), plus resource-specific filters as query parameters (`?categoryId=`, `?isActive=`), `sort` (`?sort=name` / `?sort=-createdAt`), and `q` for free-text search where applicable.
- Response envelope for lists includes `items`, `page`, `pageSize`, `totalCount` — **the total count uses the exact same filter predicate as the paged query itself**, a specific discipline called out because the developer's own SQL practice surfaced this as a real mistake class (count query silently using different filters than the paged query, producing an inconsistent "page 3 of 5" that doesn't match what's actually returned).
- Keyset/cursor pagination is evaluated specifically for the sales/audit activity feed once real access patterns exist (Phase 2/3) rather than assumed up front — recorded as its own ADR when decided.
- No endpoint returns an entire table unbounded; every list endpoint has a maximum `pageSize`.

## DTOs

- Every request/response body is an explicit DTO type — no EF Core entity is ever serialized directly.
- Create/Update DTOs are distinct from Read DTOs where the fields genuinely differ (e.g., a Read DTO includes computed/audit fields a Create DTO never accepts).

## Idempotency

- The checkout endpoint (`POST /api/v1/sales-orders`) requires an `Idempotency-Key` request header; a retried request with the same key and same store returns the original result rather than creating a duplicate sale — backed by the unique `(StoreId, IdempotencyKey)` constraint in [data-architecture.md](../architecture/data-architecture.md).

## Optimistic Concurrency

- Mutating endpoints for concurrency-sensitive resources (`InventoryItem` adjustments) accept an `If-Match` header (or a `rowVersion` field in the body) and return 409 if it doesn't match the current value — see [data-architecture.md](../architecture/data-architecture.md) Concurrency Design.

## Correlation ID

- `X-Correlation-Id` accepted from the client if present, otherwise generated server-side; echoed back on every response (success and error) and attached to every log line produced while handling that request.

## Authentication

- `Authorization: Bearer <jwt>` on every protected endpoint. See [architecture/authentication-flow.md](../architecture/authentication-flow.md).
