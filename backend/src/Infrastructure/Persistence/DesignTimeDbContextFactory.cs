using Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` construct GroceryDbContext outside the
/// DI container. Uses a stub, unauthenticated ITenantContext — safe
/// because migration generation only inspects the model shape, it never
/// executes a query filter.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GroceryDbContext>
{
    public GroceryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? "Server=localhost,1434;Database=QuickStock;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<GroceryDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new GroceryDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid StoreId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }
}
