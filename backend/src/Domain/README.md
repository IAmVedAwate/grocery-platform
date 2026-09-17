# Domain

Entities, value objects, and business rules for every module (Store, User, Product, InventoryItem, StockMovement, Supplier, PurchaseOrder, SalesOrder, ...). Pure C# — no EF Core, no ASP.NET Core, no external SDK references, enforced by an architecture test.

See [docs/architecture/data-architecture.md](../../../docs/architecture/data-architecture.md) for the entity catalog and [docs/business/business-rules-catalog.md](../../../docs/business/business-rules-catalog.md) for the rules this layer must enforce.

Not yet scaffolded — first code lands in Phase 1 (Slice 0) per [docs/ROADMAP.md](../../../docs/ROADMAP.md).
