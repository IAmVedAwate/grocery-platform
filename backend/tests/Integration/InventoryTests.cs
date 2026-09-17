using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class InventoryTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task AdjustStock_IncreasesQuantity_AndAppearsInHistory()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "inventory-adjust");
        var product = await CreateProductAsync(client, store.AccessToken, "ADJ-1");

        await AdjustStockAsync(client, store.AccessToken, product.Id, 20, "initial stock");

        var history = await Authorized(client, HttpMethod.Get, $"/api/v1/inventory/{product.Id}/history", store.AccessToken);
        history.EnsureSuccessStatusCode();
        var movements = await history.Content.ReadFromJsonAsync<List<StockMovementDto>>();

        Assert.Single(movements!);
        Assert.Equal("Adjustment", movements![0].Type);
        Assert.Equal(20, movements[0].QuantityDelta);
        Assert.Equal(20, movements[0].ResultingQuantity);
    }

    [Fact]
    public async Task AdjustStock_BelowZero_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "inventory-negative");
        var product = await CreateProductAsync(client, store.AccessToken, "ADJ-2");

        var response = await Authorized(client, HttpMethod.Post, $"/api/v1/inventory/{product.Id}/adjust", store.AccessToken,
            new { quantityDelta = -5, reason = "should fail" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ListOverview_ShowsProductsWithNoStockRecordYet_AsZero()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "inventory-overview");
        await CreateProductAsync(client, store.AccessToken, "NEVER-STOCKED");

        var response = await Authorized(client, HttpMethod.Get, "/api/v1/inventory?pageSize=50", store.AccessToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("NEVER-STOCKED", body);
        Assert.Contains("\"quantityOnHand\":0", body);
    }
}
