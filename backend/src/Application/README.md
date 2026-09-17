# Application

Use cases per business module (Identity, Catalog, Inventory, Purchasing, Sales, Customers, Reporting, Ai), organized by module folder. Defines the interfaces (`IProductRepository`, `IStorageService`, `ILlmClient`, `IUnitOfWork`, ...) that `Infrastructure` implements. Depends only on `Domain`.

See [docs/architecture/backend-architecture.md](../../../docs/architecture/backend-architecture.md) for the dependency-direction rule and module organization.

Not yet scaffolded — first code lands in Phase 1 (Slice 0) per [docs/ROADMAP.md](../../../docs/ROADMAP.md).
