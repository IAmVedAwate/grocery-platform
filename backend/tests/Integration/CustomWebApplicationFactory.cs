using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Integration;

/// <summary>
/// Real SQL Server in a container (Testcontainers), not an in-memory
/// database or a mock — see docs/testing/testing-strategy.md for why. One
/// container is shared across the whole Integration test run via
/// IntegrationTestCollection; individual tests use unique slugs/emails
/// (Guid-suffixed) so they don't interfere with each other on the shared
/// database.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "quickstock-test-storage", Guid.NewGuid().ToString("N"));

    public const string TestJwtSigningKey = "integration-test-signing-key-do-not-use-in-production-32bytes!";
    public const string TestJwtIssuer = "quickstock-api-test";
    public const string TestJwtAudience = "quickstock-client-test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONNECTION_STRING"] = _container.GetConnectionString(),
                ["JWT_SIGNING_KEY"] = TestJwtSigningKey,
                ["JWT_ISSUER"] = TestJwtIssuer,
                ["JWT_AUDIENCE"] = TestJwtAudience,
                ["ALLOWED_WEB_ORIGIN"] = "http://localhost:3000,http://localhost:3001",
                // The whole shared test run makes far more than 10
                // auth requests a minute — that's the point of a shared
                // container (docs/testing/testing-strategy.md), not abuse.
                // RateLimitingTests overrides this back down per-test via
                // WithWebHostBuilder to actually prove 429 triggers.
                ["RATE_LIMIT_AUTH_PERMIT_LIMIT"] = "100000",
                ["RATE_LIMIT_AUTH_WINDOW_SECONDS"] = "60",
                ["STORAGE_LOCAL_PATH"] = _storageRoot
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroceryDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }
}
