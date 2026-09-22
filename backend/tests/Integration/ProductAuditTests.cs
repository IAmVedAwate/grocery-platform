using Infrastructure.Persistence;
using Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// The first test coverage AuditLogEntry has had in this suite — there's
/// no GET endpoint for the audit log yet, so assertions resolve
/// GroceryDbContext directly from the factory's container. That scope has
/// no HttpContext, so ITenantContext.StoreId is Guid.Empty and the normal
/// tenant query filter would silently return nothing — same reasoning as
/// LowStockNotificationTests, so IgnoreQueryFilters() is used here too.
/// </summary>
[Collection("Integration")]
public class ProductAuditTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ChangingPrice_WritesAnAuditEntry_WithOldAndNewValues()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "audit-price");
        var product = await CreateProductAsync(client, store.AccessToken, "AUDIT-1", price: 100m, taxRatePercent: 5m);

        var update = await Authorized(client, HttpMethod.Put, $"/api/v1/products/{product.Id}", store.AccessToken,
            body: new { name = product.Name, price = 150m, taxRatePercent = 5m, barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0 });
        update.EnsureSuccessStatusCode();

        var entry = await GetAuditEntry(product.Id, "catalog.price_changed");
        Assert.NotNull(entry);
        Assert.Contains("\"previousPrice\":100", entry!.MetadataJson);
        Assert.Contains("\"newPrice\":150", entry.MetadataJson);
    }

    [Fact]
    public async Task ChangingNameOnly_DoesNotWriteAPriceChangeAuditEntry()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "audit-noprice");
        var product = await CreateProductAsync(client, store.AccessToken, "AUDIT-2", price: 75m, taxRatePercent: 0m);

        var update = await Authorized(client, HttpMethod.Put, $"/api/v1/products/{product.Id}", store.AccessToken,
            body: new { name = "Renamed Product", price = 75m, taxRatePercent = 0m, barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0 });
        update.EnsureSuccessStatusCode();

        Assert.Null(await GetAuditEntry(product.Id, "catalog.price_changed"));
    }

    private async Task<Domain.Audit.AuditLogEntry?> GetAuditEntry(Guid entityId, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroceryDbContext>();
        return await db.AuditLogEntries.IgnoreQueryFilters()
            .Where(a => a.EntityId == entityId.ToString() && a.Action == action)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }
}
