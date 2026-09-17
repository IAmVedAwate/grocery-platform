using System.Net.Http.Json;
using Api.Controllers;

namespace Integration.Helpers;

/// <summary>Shared low-level request helper + product/stock setup used
/// across the Phase 2 integration suites.</summary>
public static class ApiTestHelper
{
    public static async Task<HttpResponseMessage> Authorized(HttpClient client, HttpMethod method, string url, string accessToken, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", accessToken);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    public static async Task<ProductDto> CreateProductAsync(HttpClient client, string accessToken, string sku, decimal price = 100m, decimal taxRatePercent = 5m)
    {
        var response = await Authorized(client, HttpMethod.Post, "/api/v1/products", accessToken, new
        {
            sku, name = $"Test Product {sku}", price, taxRatePercent,
            barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    /// <summary>Gives a product initial stock via a direct adjustment —
    /// the simplest path for test setup; the purchasing-flow tests cover
    /// receiving stock through a real purchase order separately.</summary>
    public static async Task AdjustStockAsync(HttpClient client, string accessToken, Guid productId, int quantityDelta, string reason = "test setup")
    {
        var response = await Authorized(client, HttpMethod.Post, $"/api/v1/inventory/{productId}/adjust", accessToken,
            new { quantityDelta, reason });
        response.EnsureSuccessStatusCode();
    }
}
