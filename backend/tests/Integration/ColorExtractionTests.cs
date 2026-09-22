using System.Net.Http.Json;
using Integration.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// Exercises the real ONNX model (Infrastructure/ColorExtraction) — no
/// mocking, same philosophy as every other integration test in this
/// project. Assertions are loose (dominant channel comparisons, not exact
/// hex matches) since this is a genuinely trained model, not a
/// deterministic formula.
/// </summary>
[Collection("Integration")]
public class ColorExtractionTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task UploadingASolidRedImage_SetsARedDominantColor()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "color-red");
        var product = await CreateProductAsync(client, store.AccessToken, "COLOR-1");

        var png = SolidColorPng(new Rgb24(220, 20, 20));
        (await UploadImage(client, store.AccessToken, product.Id, png)).EnsureSuccessStatusCode();

        var hex = await GetDominantColorHex(client, store.AccessToken, "COLOR-1");
        Assert.NotNull(hex);
        var (r, g, b) = ParseHex(hex!);
        Assert.True(r > g && r > b, $"Expected red to dominate for hex {hex} (r={r}, g={g}, b={b}).");
    }

    [Fact]
    public async Task UploadingASolidBlueImage_SetsABlueDominantColor()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "color-blue");
        var product = await CreateProductAsync(client, store.AccessToken, "COLOR-2");

        var png = SolidColorPng(new Rgb24(20, 20, 220));
        (await UploadImage(client, store.AccessToken, product.Id, png)).EnsureSuccessStatusCode();

        var hex = await GetDominantColorHex(client, store.AccessToken, "COLOR-2");
        Assert.NotNull(hex);
        var (r, g, b) = ParseHex(hex!);
        Assert.True(b > r && b > g, $"Expected blue to dominate for hex {hex} (r={r}, g={g}, b={b}).");
    }

    [Fact]
    public async Task RemovingTheImage_AlsoClearsTheDominantColor()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "color-remove");
        var product = await CreateProductAsync(client, store.AccessToken, "COLOR-3");

        (await UploadImage(client, store.AccessToken, product.Id, SolidColorPng(new Rgb24(20, 220, 20)))).EnsureSuccessStatusCode();
        Assert.NotNull(await GetDominantColorHex(client, store.AccessToken, "COLOR-3"));

        var delete = await Authorized(client, HttpMethod.Delete, $"/api/v1/products/{product.Id}/image", store.AccessToken);
        delete.EnsureSuccessStatusCode();

        Assert.Null(await GetDominantColorHex(client, store.AccessToken, "COLOR-3"));
    }

    [Fact]
    public async Task ProductWithNoImage_HasNoDominantColor()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "color-none");
        await CreateProductAsync(client, store.AccessToken, "COLOR-4");

        Assert.Null(await GetDominantColorHex(client, store.AccessToken, "COLOR-4"));
    }

    private static byte[] SolidColorPng(Rgb24 color)
    {
        using var image = new Image<Rgb24>(64, 64, color);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static (int R, int G, int B) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            Convert.ToInt32(hex[..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16));
    }

    private static async Task<HttpResponseMessage> UploadImage(HttpClient client, string accessToken, Guid productId, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "solid.png");

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{productId}/image") { Content = content };
        request.Headers.Authorization = new("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<string?> GetDominantColorHex(HttpClient client, string accessToken, string sku)
    {
        var response = await Authorized(client, HttpMethod.Get, $"/api/v1/products?search={sku}", accessToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<ProductDtoLike>>();
        return Assert.Single(page!.Items).DominantColorHex;
    }

    private sealed record ProductDtoLike(Guid Id, string Sku, string? DominantColorHex);
}
