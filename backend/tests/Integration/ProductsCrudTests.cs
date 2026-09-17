using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Application.Common;
using Integration.Helpers;
using Xunit;

namespace Integration;

[Collection("Integration")]
public class ProductsCrudTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task CreateGetUpdateDeactivate_FullLifecycle_Works()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "products-lifecycle");

        var create = await Authorized(client, HttpMethod.Post, "/api/v1/products", store.AccessToken, new
        {
            sku = "SUGAR-1KG",
            name = "Sugar 1kg",
            price = 55.00m,
            taxRatePercent = 5m,
            barcode = "1234567890123",
            categoryId = (Guid?)null,
            brandId = (Guid?)null,
            unitId = (Guid?)null,
            lowStockThreshold = 20
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<ProductDto>();

        var get = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{created!.Id}", store.AccessToken);
        get.EnsureSuccessStatusCode();

        var update = await Authorized(client, HttpMethod.Put, $"/api/v1/products/{created.Id}", store.AccessToken, new
        {
            name = "Sugar 1kg (Updated)",
            price = 60.00m,
            taxRatePercent = 5m,
            barcode = "1234567890123",
            categoryId = (Guid?)null,
            brandId = (Guid?)null,
            unitId = (Guid?)null,
            lowStockThreshold = 15
        });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("Sugar 1kg (Updated)", updated!.Name);
        Assert.Equal(60.00m, updated.Price);

        var deactivate = await Authorized(client, HttpMethod.Delete, $"/api/v1/products/{created.Id}", store.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var afterDeactivate = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{created.Id}", store.AccessToken);
        var afterDeactivateDto = await afterDeactivate.Content.ReadFromJsonAsync<ProductDto>();
        Assert.False(afterDeactivateDto!.IsActive);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateBarcodeInSameStore_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "products-dup-barcode");
        var barcode = $"DUP-{Guid.NewGuid():N}"[..20];

        var first = await Authorized(client, HttpMethod.Post, "/api/v1/products", store.AccessToken, new
        {
            sku = "A-1", name = "Product A", price = 10m, taxRatePercent = 0m, barcode,
            categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0
        });
        first.EnsureSuccessStatusCode();

        var second = await Authorized(client, HttpMethod.Post, "/api/v1/products", store.AccessToken, new
        {
            sku = "A-2", name = "Product B", price = 20m, taxRatePercent = 0m, barcode,
            categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ListProducts_TotalCount_MatchesTheSameFilterAsTheItems()
    {
        // Regression guard for the exact mistake class called out in
        // docs/api/api-conventions.md: the count query must use the same
        // predicate as the paged query, not a looser/different one.
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "products-paging");

        for (var i = 0; i < 3; i++)
        {
            var create = await Authorized(client, HttpMethod.Post, "/api/v1/products", store.AccessToken, new
            {
                sku = $"PAGE-{i}",
                name = $"Paged Product {i}",
                price = 10m,
                taxRatePercent = 0m,
                barcode = (string?)null,
                categoryId = (Guid?)null,
                brandId = (Guid?)null,
                unitId = (Guid?)null,
                lowStockThreshold = 0
            });
            create.EnsureSuccessStatusCode();
        }

        var page1 = await Authorized(client, HttpMethod.Get, "/api/v1/products?page=1&pageSize=2", store.AccessToken);
        var page1Result = await page1.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();

        Assert.Equal(2, page1Result!.Items.Count);
        Assert.Equal(3, page1Result.TotalCount);
    }

    private static async Task<HttpResponseMessage> Authorized(HttpClient client, HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
