using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Integration;

/// <summary>
/// Genuinely fault-injecting this (pausing the shared Testcontainers SQL
/// instance mid-query) was considered and deliberately rejected: the whole
/// Integration collection shares one container sequentially
/// (CustomWebApplicationFactory), and a paused/hung connection risks
/// destabilizing every other test's timing for a property this can verify
/// more directly. What's actually being claimed — "the app's real DI
/// registration wires up SQL Server connection resiliency" — is a
/// configuration fact, not a runtime behavior, so it's verified as one:
/// resolving the real GroceryDbContext through the real container and
/// checking EF Core actually built a retrying execution strategy, not a
/// hand-constructed DbContextOptionsBuilder that might not match what
/// Program.cs really registers.
/// </summary>
[Collection("Integration")]
public class SqlResiliencyTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public void GroceryDbContext_UsesARetryingExecutionStrategy()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroceryDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();

        Assert.IsType<SqlServerRetryingExecutionStrategy>(strategy);
    }
}
