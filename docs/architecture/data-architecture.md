# Data Architecture

Authoritative table-by-table data model. Engine: SQL Server (local: SQL Server 2025 in Docker; cloud: Azure SQL, [ADR pending Phase 4]). ORM: EF Core, migrations owned by `Infrastructure`.

## Multi-Tenancy Convention

Every tenant-owned table below carries a non-nullable `StoreId` (FK to `Store.Id`) unless explicitly marked **Global**. Convention enforced by:
- EF Core global query filter: `HasQueryFilter(e => e.StoreId == _tenantContext.StoreId)` applied to every tenant-scoped entity configuration.
- A composite index leading with `StoreId` wherever the table is queried by tenant (nearly always).
- An architecture/integration test that fails the build if a new tenant-owned `DbSet` is added without a corresponding query filter.

See [ADR-002](../decisions/ADR-002-multi-tenancy-strategy.md) for the reasoning.

## Core Tables

### Store (Global — this *is* the tenant root)
`Id (PK)`, `Name`, `Slug` (unique), `Status` (Active/Suspended), `CreatedAtUtc`.

### User
`Id (PK)`, `StoreId (FK)`, `Email` (unique per store), `PasswordHash` (via Identity), `DisplayName`, `IsActive`, `CreatedAtUtc`.
Indexes: `(StoreId, Email)` unique.

### Role / Permission / RolePermission / UserRole
`Role(Id, StoreId, Name)`; `Permission(Id, Key)` — global catalog of permission strings (`inventory.read`, `sales.refund`, ...); `RolePermission(RoleId, PermissionId)`; `UserRole(UserId, RoleId)`. Permission catalog is global (not tenant-scoped) since the *set of possible permissions* is fixed by the application, not per-store; role-to-permission assignment is tenant-scoped so a store could in principle customize a role's permission bundle.

### RefreshToken
`Id (PK)`, `UserId (FK)`, `TokenHash`, `FamilyId`, `ExpiresAtUtc`, `RevokedAtUtc (nullable)`, `ReplacedByTokenId (nullable)`. Enables rotation + reuse detection (see [ADR-004](../decisions/ADR-004-authentication-architecture.md)).

### Category / Brand / Unit
`Id (PK)`, `StoreId (FK)`, `Name`, `IsActive`. Simple lookups, no deep hierarchy in core scope.

### Product
`Id (PK)`, `StoreId (FK)`, `Sku`, `Barcode` (nullable, indexed), `Name`, `CategoryId (FK)`, `BrandId (FK)`, `UnitId (FK)`, `Price (decimal)`, `TaxRatePercent (decimal)`, `IsActive`, `LowStockThreshold`, `CreatedAtUtc`, `UpdatedAtUtc`.
Indexes: `(StoreId, Barcode)` unique where not null (fast checkout scan lookup); `(StoreId, Sku)` unique; `(StoreId, IsActive, Name)` for catalog list/search.

### InventoryItem
`Id (PK)`, `StoreId (FK)`, `ProductId (FK, unique per store)`, `QuantityOnHand (int)`, `RowVersion (concurrency token)`, `UpdatedAtUtc`.
The `RowVersion` is the enforcement point for the concurrency scenario in [PRD §23](../PRD.md#7-acceptance-criteria) — see below.

### StockMovement (append-only)
`Id (PK)`, `StoreId (FK)`, `InventoryItemId (FK)`, `Type` (Receipt/Sale/Adjustment/Transfer), `QuantityDelta (int, signed)`, `ReferenceType` + `ReferenceId` (e.g., links to the `SalesOrder` or `GoodsReceipt` that caused it), `Reason` (nullable, required for Adjustment), `ActorUserId`, `CreatedAtUtc`, `ResultingQuantity`.
Indexes: `(StoreId, InventoryItemId, CreatedAtUtc)` for history/audit queries.

### Supplier
`Id (PK)`, `StoreId (FK)`, `Name`, `ContactInfo`, `Status` (Active/Inactive), `PaymentTermsDays (nullable)`.

### PurchaseOrder / PurchaseOrderItem
`PurchaseOrder(Id, StoreId, SupplierId, Status [Draft/Submitted/Approved/PartiallyReceived/Received/Cancelled], ApprovedByUserId (nullable), CreatedAtUtc)`.
`PurchaseOrderItem(Id, PurchaseOrderId, ProductId, QuantityOrdered, UnitCost, QuantityReceived)`.

### GoodsReceipt / GoodsReceiptItem
`GoodsReceipt(Id, StoreId, PurchaseOrderId, ReceivedByUserId, CreatedAtUtc)`.
`GoodsReceiptItem(Id, GoodsReceiptId, PurchaseOrderItemId, QuantityReceived)` — each receipt generates matching `StockMovement` rows of type `Receipt`.

### Customer
`Id (PK)`, `StoreId (FK)`, `Name`, `Phone (nullable)`, `Email (nullable)`, `CreatedAtUtc`.

### SalesOrder / SalesOrderItem / Payment / Invoice
`SalesOrder(Id, StoreId, CustomerId (nullable), Status [Draft/Completed/Refunded/PartiallyRefunded], SubtotalAmount, TaxAmount, DiscountAmount, TotalAmount, IdempotencyKey (unique per store), CreatedByUserId, CreatedAtUtc)`.
`SalesOrderItem(Id, SalesOrderId, ProductId, Quantity, UnitPrice, TaxAmount, LineDiscount)`.
`Payment(Id, SalesOrderId, Method, Amount, Status, CreatedAtUtc)`.
`Invoice(Id, SalesOrderId, InvoiceNumber (unique per store), IssuedAtUtc)`.
Indexes: `(StoreId, IdempotencyKey)` unique — backs the idempotent checkout requirement in [PRD §11](../PRD.md#11-api-requirements).

### AuditLog (append-only, Global table with a StoreId column for filtering — see note below)
`Id (PK)`, `StoreId (FK, nullable for platform-level events)`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `MetadataJson`, `CorrelationId`, `CreatedAtUtc`.
No `Update`/`Delete` DbSet operations are exposed for this entity above `Infrastructure`.

### Notification
`Id (PK)`, `StoreId (FK)`, `Type`, `Payload`, `IsRead`, `CreatedAtUtc`.

### Document / DocumentChunk (Phase 5, as built — see [ai-architecture.md](./ai-architecture.md))
`Document(Id, StoreId, FileName [nvarchar 260], ContentType [nvarchar 100], StorageKey [nvarchar 128], UploadedByUserId, UploadedAtUtc)` — a normal EF Core entity/table, global-query-filtered by `StoreId` like every other tenant-owned table. The raw file bytes live in `IStorageService` (same local/Azure-Blob abstraction as product images); `StorageKey` is the pointer.

`DocumentChunk(Id [string, "{documentId}:{chunkIndex}"], StoreId, DocumentId, FileName, ChunkIndex, Content, Vector)` is **not** an EF Core entity — it's a record type mapped via `CommunityToolkit.VectorData` attributes (`[VectorStoreKey]`, `[VectorStoreData(IsIndexed = true)]` on `StoreId`, `[VectorStoreVector(1536)]` on `Vector`) into a `VectorStore` collection (`document_chunks`) that CommunityToolkit.VectorData.SqlServer backs with a SQL Server 2025 native `vector` column — in the *same* database, over the *same* connection string, as every relational table (ADR-003), just addressed through a different persistence API than `GroceryDbContext`. `Vector` holds the chunk's own text; the collection's configured `EmbeddingGenerator` embeds it automatically on upsert. `StoreId` is `IsIndexed` specifically so `VectorSearchOptions.Filter` can apply the tenant boundary as a real pre-filter on the similarity search itself, not a post-filter — see [ai-architecture.md](./ai-architecture.md) and `tests/Integration/SearchDocumentsTenantIsolationTests.cs`.

### AiInteractionLog — not built
Originally planned as a platform-level interaction log (prompt, tool calls, latency, outcome). Not implemented in Phase 5 — standard structured request logging (Serilog + correlation IDs) covers every AI endpoint the same as any other, but there's no dedicated interaction-level record. Documented here as a known gap (see [ai-architecture.md](./ai-architecture.md#known-gaps-honestly-scoped-out-of-phase-5)) rather than left silently implied by this doc.

### SubscriptionPlan (Global — schema stub only, no logic in core scope)
`Id (PK)`, `StoreId (FK)`, `PlanName`, `Status`. Exists so the schema doesn't need a breaking migration if billing is added post-MVP; not read or written by any Phase 1–5 feature.

## Concurrency Design

`InventoryItem.RowVersion` is a SQL Server `rowversion` column. The checkout use case reads the `InventoryItem` inside the same transaction as the sale, decrements `QuantityOnHand`, and saves with the original `RowVersion` as the concurrency check. EF Core translates this into an `UPDATE ... WHERE Id = @id AND RowVersion = @originalRowVersion`; if a concurrent transaction already updated the row, zero rows are affected, EF Core raises `DbUpdateConcurrencyException`, and the Application layer maps that to a 409 Conflict — the "last unit, two cashiers" scenario resolves to exactly one winner without a database-level lock held for the request duration. See [ADR-004](../decisions/ADR-004-authentication-architecture.md) sibling reasoning and the checkout workflow test in [testing-strategy.md](../testing/testing-strategy.md).

## Indexing Principles

- Every tenant-scoped query pattern gets a composite index leading with `StoreId`.
- Report queries (sales by day/product/category) are indexed to support seeks, not scans, at the dataset sizes defined in the testing strategy — verified with execution plans during Phase 3, not assumed at design time.
- Uniqueness constraints double as indexes where they naturally align with a hot query (e.g., `(StoreId, Barcode)`).

## What Is Deliberately Not Modeled Yet

Multi-location inventory (the schema's `InventoryItem` is per-store, not per-store-per-location) is intentionally deferred — the PRD anticipates it (§14 of the original brief) but the core 5 phases model a single location per store to avoid speculative complexity. Adding a `LocationId` to `InventoryItem` later is a additive migration, not a redesign, because `StoreId`-first scoping is already the pattern throughout.
