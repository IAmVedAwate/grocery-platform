using Application.Common;
using Domain;
using Domain.Catalog;
using Infrastructure.Identity;
using Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Infrastructure.Persistence;

/// <summary>
/// Single DbContext for the modular monolith (docs/architecture/backend-architecture.md).
/// Extends IdentityDbContext so ASP.NET Core Identity's own tables
/// (AspNetUsers, AspNetRoles, ...) live in the same schema as the business
/// tables — one database, one migration history, per ADR-002.
/// </summary>
public class GroceryDbContext(DbContextOptions<GroceryDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IUnitOfWork
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Domain.Catalog.Unit> Units => Set<Domain.Catalog.Unit>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<UserPermissionEntity> UserPermissions => Set<UserPermissionEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<Domain.Audit.AuditLogEntry> AuditLogEntries => Set<Domain.Audit.AuditLogEntry>();
    public DbSet<Domain.Inventory.InventoryItem> InventoryItems => Set<Domain.Inventory.InventoryItem>();
    public DbSet<Domain.Inventory.StockMovement> StockMovements => Set<Domain.Inventory.StockMovement>();
    public DbSet<Domain.Purchasing.Supplier> Suppliers => Set<Domain.Purchasing.Supplier>();
    public DbSet<Domain.Purchasing.PurchaseOrder> PurchaseOrders => Set<Domain.Purchasing.PurchaseOrder>();
    public DbSet<Domain.Purchasing.PurchaseOrderItem> PurchaseOrderItems => Set<Domain.Purchasing.PurchaseOrderItem>();
    public DbSet<Domain.Sales.Customer> Customers => Set<Domain.Sales.Customer>();
    public DbSet<Domain.Sales.SalesOrder> SalesOrders => Set<Domain.Sales.SalesOrder>();
    public DbSet<Domain.Sales.SalesOrderItem> SalesOrderItems => Set<Domain.Sales.SalesOrderItem>();
    public DbSet<Domain.Sales.Payment> Payments => Set<Domain.Sales.Payment>();
    public DbSet<Domain.Sales.Invoice> Invoices => Set<Domain.Sales.Invoice>();
    public DbSet<Domain.Notifications.Notification> Notifications => Set<Domain.Notifications.Notification>();
    public DbSet<Domain.Ai.Document> Documents => Set<Domain.Ai.Document>();

    /// <summary>
    /// Translates EF Core's DbUpdateConcurrencyException into the
    /// application's own ConflictAppException here, once, so every
    /// caller of IUnitOfWork.SaveChangesAsync — Sales checkout racing on
    /// InventoryItem.RowVersion being the important case
    /// (docs/architecture/data-architecture.md Concurrency Design) — gets
    /// a clean 409 without Application code ever referencing EF Core
    /// (only Infrastructure is allowed to, per docs/architecture/backend-architecture.md).
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictAppException("The data was modified by another request; please retry.");
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new StoreConfiguration());
        builder.ApplyConfiguration(new ProductConfiguration());

        // IMPORTANT — found the hard way (docs/operations/troubleshooting.md):
        // every HasQueryFilter below references `tenantContext` as a member
        // access rooted at `this` (GroceryDbContext), because it's a primary
        // constructor parameter captured into a private instance field by
        // the compiler. EF Core specifically rebinds `this.<field>` accesses
        // per DbContext INSTANCE at query time. If the filter is instead
        // defined inside a separate IEntityTypeConfiguration<T> class that
        // captures its OWN copy of ITenantContext (as ProductConfiguration
        // originally did), the filter closes over THAT INSTANCE's field —
        // and because OnModelCreating only runs ONCE per DbContext type
        // (the compiled model is cached for the app's lifetime), every
        // later request's query silently keeps using the tenant from the
        // very FIRST request that triggered model creation. Writes were
        // unaffected (Application code reads ITenantContext directly, per
        // request) — only reads through the filter were stale, which is
        // exactly why this passed a naive smoke test and only broke on a
        // second store.
        builder.Entity<Product>().HasQueryFilter(p => p.StoreId == tenantContext.StoreId);

        builder.Entity<Category>(b =>
        {
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.StoreId, c.Name });
            b.HasQueryFilter(c => c.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Brand>(b =>
        {
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.StoreId, c.Name });
            b.HasQueryFilter(c => c.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Catalog.Unit>(b =>
        {
            b.Property(c => c.Name).IsRequired().HasMaxLength(50);
            b.HasIndex(c => new { c.StoreId, c.Name });
            b.HasQueryFilter(c => c.StoreId == tenantContext.StoreId);
        });

        // ApplicationUser/ApplicationRole deliberately have NO query filter —
        // see the comment on ApplicationUser for why.
        //
        // IdentityDbContext's base OnModelCreating (called above) creates a
        // GLOBALLY unique index on NormalizedUserName ("UserNameIndex") and
        // NormalizedName ("RoleNameIndex") — the same multi-tenancy bug as
        // the default validators (docs/operations/troubleshooting.md), just
        // at the schema layer. Both are overridden to non-unique here, with
        // a (StoreId, Normalized...) composite unique index taking their
        // place, so two different stores can each have their own "Admin"
        // role or an admin with the same email.
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
            b.HasIndex(u => u.NormalizedUserName).IsUnique(false);
            b.HasIndex(u => new { u.StoreId, u.NormalizedUserName }).IsUnique();
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.HasIndex(r => r.NormalizedName).IsUnique(false);
            b.HasIndex(r => new { r.StoreId, r.NormalizedName }).IsUnique();
        });

        builder.Entity<PermissionEntity>(b =>
        {
            b.HasKey(p => p.Key);
            b.Property(p => p.Key).HasMaxLength(64);
            // The permission catalog is fixed in code (Domain.Identity.Permissions),
            // not runtime-editable, so it's seeded once at migration time rather
            // than upserted defensively at every role-creation call.
            b.HasData(Domain.Identity.Permissions.All.Select(key => new PermissionEntity { Key = key }));
        });

        builder.Entity<RolePermissionEntity>(b =>
        {
            b.HasKey(rp => new { rp.RoleId, rp.PermissionKey });
            b.HasOne<ApplicationRole>().WithMany().HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<PermissionEntity>().WithMany().HasForeignKey(rp => rp.PermissionKey).OnDelete(DeleteBehavior.Cascade);
        });

        // The authoritative per-user permission set (see UserPermissionEntity's
        // own remarks) — IdentityServiceImpl.GetPermissionsAsync reads
        // exclusively from this table, not from role membership.
        builder.Entity<UserPermissionEntity>(b =>
        {
            b.HasKey(up => new { up.UserId, up.PermissionKey });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<PermissionEntity>().WithMany().HasForeignKey(up => up.PermissionKey).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshTokenEntity>(b =>
        {
            b.HasKey(rt => rt.Id);
            b.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(256);
            b.HasIndex(rt => rt.TokenHash).IsUnique();
            b.HasIndex(rt => rt.FamilyId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Phase 2: Audit ---
        builder.Entity<Domain.Audit.AuditLogEntry>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Action).IsRequired().HasMaxLength(100);
            b.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            b.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
            b.HasIndex(a => new { a.StoreId, a.CreatedAtUtc });
            b.HasQueryFilter(a => a.StoreId == tenantContext.StoreId);
        });

        // --- Phase 2: Inventory ---
        builder.Entity<Domain.Inventory.InventoryItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.RowVersion).IsRowVersion();
            b.HasIndex(i => new { i.StoreId, i.ProductId }).IsUnique();
            b.HasQueryFilter(i => i.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Inventory.StockMovement>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
            b.Property(m => m.Reason).HasMaxLength(500);
            b.Property(m => m.ReferenceType).HasMaxLength(100);
            b.HasIndex(m => new { m.StoreId, m.InventoryItemId, m.CreatedAtUtc });
            b.HasOne<Domain.Inventory.InventoryItem>().WithMany().HasForeignKey(m => m.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(m => m.StoreId == tenantContext.StoreId);
        });

        // --- Phase 2: Purchasing ---
        builder.Entity<Domain.Purchasing.Supplier>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(s => new { s.StoreId, s.Name });
            b.HasQueryFilter(s => s.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Purchasing.PurchaseOrder>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(o => new { o.StoreId, o.Status });
            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasOne<Domain.Purchasing.Supplier>().WithMany().HasForeignKey(o => o.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(o => o.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Purchasing.PurchaseOrderItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");
        });

        // --- Phase 2: Sales ---
        builder.Entity<Domain.Sales.Customer>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.StoreId, c.Name });
            b.HasQueryFilter(c => c.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Sales.SalesOrder>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(o => o.SubtotalAmount).HasColumnType("decimal(18,2)");
            b.Property(o => o.TaxAmount).HasColumnType("decimal(18,2)");
            b.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)");
            b.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
            b.Property(o => o.IdempotencyKey).IsRequired().HasMaxLength(100);
            b.HasIndex(o => new { o.StoreId, o.IdempotencyKey }).IsUnique();
            // Supports every Reporting query (docs/architecture/sql-performance-pass.md)
            // and SalesOrderRepository.ListAsync's own CreatedAtUtc ordering — every
            // one of them filters StoreId first (via the query filter below) and
            // CreatedAtUtc second, so that's the index's column order too, turning what
            // was a full clustered index scan into a seek.
            b.HasIndex(o => new { o.StoreId, o.CreatedAtUtc });
            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasOne(o => o.Payment).WithOne().HasForeignKey<Domain.Sales.Payment>(p => p.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(o => o.Invoice).WithOne().HasForeignKey<Domain.Sales.Invoice>(i => i.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Domain.Sales.Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(o => o.StoreId == tenantContext.StoreId);
        });

        builder.Entity<Domain.Sales.SalesOrderItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
            b.Property(i => i.LineDiscount).HasColumnType("decimal(18,2)");

            // Covering indexes (docs/architecture/sql-performance-pass.md):
            // every column ReportingRepository's per-product/per-category
            // aggregates read (Quantity, UnitPrice, TaxAmount, LineDiscount)
            // plus the join key are INCLUDEd, so SQL Server can answer the
            // join/aggregate entirely from the narrower nonclustered index
            // without a key lookup back into the clustered index for every
            // matching row. SalesOrderId already gets a plain FK index by
            // EF Core's own convention; this replaces it with a covering
            // version instead of leaving a second, narrower duplicate.
            b.HasIndex(i => i.SalesOrderId)
                .IncludeProperties(i => new { i.ProductId, i.Quantity, i.UnitPrice, i.TaxAmount, i.LineDiscount });
            b.HasIndex(i => i.ProductId)
                .IncludeProperties(i => new { i.SalesOrderId, i.Quantity, i.UnitPrice, i.TaxAmount, i.LineDiscount });
        });

        builder.Entity<Domain.Sales.Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Domain.Sales.Invoice>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
            b.HasIndex(i => i.InvoiceNumber).IsUnique();
        });

        // --- Phase 3: Notifications ---
        builder.Entity<Domain.Notifications.Notification>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.Type).IsRequired().HasMaxLength(50);
            b.Property(n => n.Payload).IsRequired();
            // Supports LowStockNotificationGenerator's per-product dedupe
            // check and the notifications feed's unread-first ordering.
            b.HasIndex(n => new { n.StoreId, n.ReferenceId, n.IsRead });
            b.HasIndex(n => new { n.StoreId, n.IsRead, n.CreatedAtUtc });
            b.HasQueryFilter(n => n.StoreId == tenantContext.StoreId);
        });

        // --- Phase 5: AI / RAG document metadata (chunks/embeddings live
        // in the SQL Server native-vector collection instead — Infrastructure/Ai) ---
        builder.Entity<Domain.Ai.Document>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.FileName).IsRequired().HasMaxLength(260);
            b.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            b.Property(d => d.StorageKey).IsRequired().HasMaxLength(128);
            b.HasIndex(d => new { d.StoreId, d.UploadedAtUtc });
            b.HasQueryFilter(d => d.StoreId == tenantContext.StoreId);
        });
    }
}
