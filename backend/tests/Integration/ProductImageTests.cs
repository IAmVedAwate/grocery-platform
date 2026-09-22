using System.Net;
using System.Net.Http.Json;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class ProductImageTests(CustomWebApplicationFactory factory)
{
    // Smallest possible valid PNG (1x1 transparent pixel) — real bytes, not
    // a fake string, so SetImageAsync's content-type/size path is exercised
    // against actual file I/O through LocalFileStorageService.
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    [Fact]
    public async Task UploadThenGet_RoundTripsTheSameBytes()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "img-roundtrip");
        var product = await CreateProductAsync(client, store.AccessToken, "IMG-1");

        var upload = await UploadImage(client, store.AccessToken, product.Id, TinyPng, "image/png");
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var get = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{product.Id}/image", store.AccessToken);
        get.EnsureSuccessStatusCode();
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        var bytes = await get.Content.ReadAsByteArrayAsync();
        Assert.Equal(TinyPng, bytes);

        var list = await Authorized(client, HttpMethod.Get, $"/api/v1/products?search=IMG-1", store.AccessToken);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedResultDto<ProductDtoLike>>();
        Assert.True(Assert.Single(page!.Items).HasImage);
    }

    [Fact]
    public async Task RejectsUnsupportedContentType()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "img-badtype");
        var product = await CreateProductAsync(client, store.AccessToken, "IMG-2");

        var response = await UploadImage(client, store.AccessToken, product.Id, [0x00, 0x01], "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteImage_RemovesIt_AndSubsequentGetIs404()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "img-delete");
        var product = await CreateProductAsync(client, store.AccessToken, "IMG-3");

        (await UploadImage(client, store.AccessToken, product.Id, TinyPng, "image/png")).EnsureSuccessStatusCode();

        var delete = await Authorized(client, HttpMethod.Delete, $"/api/v1/products/{product.Id}/image", store.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{product.Id}/image", store.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task GetImage_ForProductWithNoImage_Returns404NotAnError()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "img-none");
        var product = await CreateProductAsync(client, store.AccessToken, "IMG-4");

        var get = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{product.Id}/image", store.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task ProductImage_IsTenantScoped()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "img-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "img-tenant-b");
        var product = await CreateProductAsync(client, storeA.AccessToken, "IMG-5");
        (await UploadImage(client, storeA.AccessToken, product.Id, TinyPng, "image/png")).EnsureSuccessStatusCode();

        var crossTenantGet = await Authorized(client, HttpMethod.Get, $"/api/v1/products/{product.Id}/image", storeB.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantGet.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadImage(HttpClient client, string accessToken, Guid productId, byte[] bytes, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "image", "upload.bin");

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{productId}/image") { Content = content };
        request.Headers.Authorization = new("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private sealed record ProductDtoLike(Guid Id, string Sku, bool HasImage);
}
