using System.Net.Http.Json;
using Application.Reporting;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class ReportingTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task SalesByProduct_AggregatesRevenueAndQuantity_AcrossMultipleSales()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "report-by-product");
        var product = await CreateProductAsync(client, store.AccessToken, "REP-1", price: 100m, taxRatePercent: 0m);
        await AdjustStockAsync(client, store.AccessToken, product.Id, 20);

        // Two separate sales of the same product: 3 units, then 2 units.
        await Checkout(client, store.AccessToken, product.Id, 3);
        await Checkout(client, store.AccessToken, product.Id, 2);

        var (from, to) = TodayRangeUtc();
        var response = await Authorized(client, HttpMethod.Get,
            $"/api/v1/reports/sales-by-product?from={from:O}&to={to:O}", store.AccessToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<SalesByProductRow>>();

        var row = Assert.Single(result!.Items, r => r.ProductId == product.Id);
        Assert.Equal(5, row.QuantitySold);
        Assert.Equal(500m, row.Revenue);
    }

    [Fact]
    public async Task SalesByCategory_GroupsProductsWithoutACategory_AsUncategorized()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "report-by-category");
        var product = await CreateProductAsync(client, store.AccessToken, "REP-2", price: 50m, taxRatePercent: 0m);
        await AdjustStockAsync(client, store.AccessToken, product.Id, 10);
        await Checkout(client, store.AccessToken, product.Id, 4);

        var (from, to) = TodayRangeUtc();
        var response = await Authorized(client, HttpMethod.Get,
            $"/api/v1/reports/sales-by-category?from={from:O}&to={to:O}", store.AccessToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<SalesByCategoryRow>>();

        var row = Assert.Single(result!.Items);
        Assert.Null(row.CategoryId);
        Assert.Equal("Uncategorized", row.CategoryName);
        Assert.Equal(200m, row.Revenue);
    }

    [Fact]
    public async Task SalesByDay_ExcludesSalesOutsideTheRequestedRange()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "report-by-day");
        var product = await CreateProductAsync(client, store.AccessToken, "REP-3");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 5);
        await Checkout(client, store.AccessToken, product.Id, 1);

        // A range entirely in the past excludes today's sale.
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var response = await Authorized(client, HttpMethod.Get,
            $"/api/v1/reports/sales-by-day?from={yesterday.AddDays(-2):O}&to={yesterday:O}", store.AccessToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<SalesByDayRow>>();

        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Reports_AreTenantScoped()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "report-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "report-tenant-b");
        var productA = await CreateProductAsync(client, storeA.AccessToken, "REP-A");
        await AdjustStockAsync(client, storeA.AccessToken, productA.Id, 5);
        await Checkout(client, storeA.AccessToken, productA.Id, 1);

        var (from, to) = TodayRangeUtc();
        var response = await Authorized(client, HttpMethod.Get,
            $"/api/v1/reports/sales-by-product?from={from:O}&to={to:O}", storeB.AccessToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<SalesByProductRow>>();

        Assert.Empty(result!.Items);
    }

    private static (DateTime From, DateTime To) TodayRangeUtc()
    {
        var now = DateTime.UtcNow;
        return (now.Date, now.Date.AddDays(1).AddTicks(-1));
    }

    private static async Task Checkout(HttpClient client, string accessToken, Guid productId, int quantity)
    {
        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", accessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId, quantity, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());
        response.EnsureSuccessStatusCode();
    }
}

// Matches Application.Common.PagedResult<T>'s JSON shape without pulling
// in a hard dependency on it from the test project.
public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
