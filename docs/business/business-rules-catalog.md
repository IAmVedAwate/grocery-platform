# Business Rules Catalog

Full catalog referenced from [PRD §8](../PRD.md#8-business-rules). Each rule states the trigger, the rule itself, and the enforcement point.

## Identity & Access

| Rule | Enforcement Point |
|---|---|
| A deactivated user cannot authenticate, even with a valid, unexpired refresh token. | Checked at both login and every refresh — see [authentication-flow.md](../architecture/authentication-flow.md). |
| A user belongs to exactly one store; there is no cross-store account. | Schema: `User.StoreId` non-nullable, no join table allowing multiple stores. |
| Privileged permissions (`users.manage`, `purchase.approve`, `sales.refund`) require an explicit permission grant, never inferred from a role name string match. | Custom `IAuthorizationHandler` per permission. |

## Catalog

| Rule | Enforcement Point |
|---|---|
| A product's barcode, if set, is unique within its store. | Unique index `(StoreId, Barcode)` where not null. |
| An inactive product cannot be added to a new sale. | `Application.Sales.CreateSaleOrder` validates product `IsActive` before accepting a line item. |

## Inventory

| Rule | Enforcement Point |
|---|---|
| Stock quantity can never go negative as a result of a sale. | Transaction + `RowVersion` concurrency check on `InventoryItem`; see [data-architecture.md](../architecture/data-architecture.md) Concurrency Design. |
| A manual stock adjustment requires `inventory.adjust` permission and a reason. | Authorization policy + `Application.Inventory.AdjustStock` validation. |
| Every stock quantity change is recorded as an immutable `StockMovement`, never a silent update to `QuantityOnHand` alone. | `InventoryItem.QuantityOnHand` is only ever changed inside the same operation that writes a corresponding `StockMovement` row. |

## Purchasing

| Rule | Enforcement Point |
|---|---|
| A purchase order must reference a valid, active supplier. | FK constraint + Application-layer validation. |
| Purchase orders above a configurable value threshold require approval before receiving. | `Application.Purchasing.SubmitPurchaseOrder` / `ApprovePurchaseOrder` state machine. |
| Receiving cannot record a quantity that would exceed the ordered quantity for a line item unless explicitly flagged as an over-receipt (not allowed by default). | `Application.Purchasing.ReceiveGoods` validation against `PurchaseOrderItem.QuantityOrdered`. |
| A cancelled purchase order cannot be received against. | Status state machine (`Draft → Submitted → Approved → [Partially]Received / Cancelled`) — invalid transitions rejected. |

## Sales

| Rule | Enforcement Point |
|---|---|
| Sale quantity must be a positive integer not exceeding available stock at commit time. | Validated inside the checkout transaction, not just at cart-build time (stock can change between adding to cart and checkout). |
| Price/discount/tax are computed server-side from the product's current configuration — never trusted from client input. | `Application.Sales.CreateSaleOrder` recomputes line totals; client-submitted price fields, if any, are ignored/validated against the server value. |
| A refund must reference a legitimate original `SalesOrder` and cannot exceed the originally sold quantity for that line. | `Application.Sales.RefundSale` validates against `SalesOrderItem.Quantity` and prior refund history. |
| A retried checkout request with the same idempotency key returns the original result, never a duplicate sale. | Unique `(StoreId, IdempotencyKey)` constraint on `SalesOrder`. |

## Cross-Cutting

| Rule | Enforcement Point |
|---|---|
| Every entity except the global tenant registry is implicitly scoped to exactly one store; no query inside a tenant's request touches another tenant's rows. | EF Core global query filters + explicit `StoreId` predicates + dedicated cross-tenant integration tests. |
| Audit records are append-only. | No `Update`/`Delete` operation exposed above `Infrastructure` for `AuditLog`/`StockMovement`. |
| Money is never represented as a floating-point type. | All monetary columns/properties are `decimal`. |

This catalog is extended as each module is implemented — new rules discovered during Phase 2–5 implementation are added here, not left implicit in code.
