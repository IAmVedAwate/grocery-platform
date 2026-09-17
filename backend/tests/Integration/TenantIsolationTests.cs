using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Application.Common;
using Integration.Helpers;
using Xunit;

namespace Integration;

/// <summary>
/// The highest-consequence test class in this project — see
/// docs/decisions/ADR-002 and docs/security/security-model.md. Every test
/// here failed at least once during development (three separate
/// multi-tenancy bugs found and fixed — see docs/operations/troubleshooting.md)
/// before this suite passed.
/// </summary>
[Collection("Integration")]
public class TenantIsolationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ProductCreatedByOneStore_IsInvisibleToAnotherStore_InListing()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "tenant-b");

        await CreateProductAsync(client, storeA.AccessToken, "RICE-5KG");

        var storeBList = await GetProductsAsync(client, storeB.AccessToken);
        Assert.Empty(storeBList!.Items);

        var storeAList = await GetProductsAsync(client, storeA.AccessToken);
        Assert.Single(storeAList!.Items);
    }

    [Fact]
    public async Task ProductCreatedByOneStore_ReturnsNotFound_WhenFetchedDirectlyByAnotherStore()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "tenant-c");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "tenant-d");

        var product = await CreateProductAsync(client, storeA.AccessToken, "OIL-1L");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/products/{product!.Id}");
        request.Headers.Authorization = new("Bearer", storeB.AccessToken);
        var response = await client.SendAsync(request);

        // 404, never 403 — a 403 would confirm the product exists at all,
        // which is itself information leakage across the tenant boundary.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TwoStoresCanEachHaveTheirOwnAdminRole_WithoutColliding()
    {
        var client = factory.CreateClient();

        // Registration itself creates an "Admin" role per store — this is
        // exactly the scenario that originally crashed with
        // "Role name 'Admin' is already taken" (docs/operations/troubleshooting.md).
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "role-clash-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "role-clash-b");

        Assert.NotEqual(storeA.StoreId, storeB.StoreId);
        Assert.NotEqual(Guid.Empty, storeA.StoreId);
        Assert.NotEqual(Guid.Empty, storeB.StoreId);
    }

    private static async Task<ProductDto?> CreateProductAsync(HttpClient client, string accessToken, string sku)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                sku,
                name = $"Test Product {sku}",
                price = 100.00m,
                taxRatePercent = 5m,
                barcode = (string?)null,
                categoryId = (Guid?)null,
                brandId = (Guid?)null,
                unitId = (Guid?)null,
                lowStockThreshold = 5
            })
        };
        request.Headers.Authorization = new("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

    private static async Task<PagedResult<ProductDto>?> GetProductsAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Authorization = new("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
    }
}
