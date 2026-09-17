using Application.Common;
using Domain;
using Domain.Catalog;
using Infrastructure.Identity;
using Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

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

        builder.Entity<RefreshTokenEntity>(b =>
        {
            b.HasKey(rt => rt.Id);
            b.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(256);
            b.HasIndex(rt => rt.TokenHash).IsUnique();
            b.HasIndex(rt => rt.FamilyId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
