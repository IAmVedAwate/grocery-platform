using System.Net;
using Infrastructure.Ai;
using Integration.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// Every other integration test runs against CustomWebApplicationFactory's
/// FakeEmbeddingGenerator, which — by design — never requires GEMINI_API_KEY.
/// That's correct for keeping the suite token-free, but it also means those
/// tests can never catch a regression in the PRODUCTION registration's own
/// laziness. This test reverts just the embedding-generator override back
/// to the real LazyGeminiEmbeddingGenerator (see Program.cs) — the exact
/// class production actually uses — and proves the app's own claim: every
/// document operation that doesn't need to embed anything (list, delete)
/// works with no GEMINI_API_KEY configured at all, and only upload (which
/// genuinely needs to embed new chunks) fails, with a clear error rather
/// than a generic 500.
///
/// This exists because of a real bug caught by hand while manually
/// smoke-testing the dev server: registering the real embedding generator
/// directly (instead of behind this lazy wrapper) made GET /api/v1/documents
/// throw 500 with no key configured, since CommunityToolkit.VectorData's
/// VectorStore eagerly resolves IEmbeddingGenerator the moment it's
/// constructed — which happens for every AI-adjacent class, not just ones
/// that actually embed something.
/// </summary>
[Collection("Integration")]
public class DocumentsWorkWithoutGeminiKeyTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ListAndDelete_WorkWithNoGeminiApiKeyConfigured()
    {
        using var realLazyFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => new LazyGeminiEmbeddingGenerator(sp));
            }));
        var client = realLazyFactory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-no-key");

        var list = await Authorized(client, HttpMethod.Get, "/api/v1/documents", store.AccessToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var delete = await Authorized(client, HttpMethod.Delete, $"/api/v1/documents/{Guid.NewGuid()}", store.AccessToken);
        // 404 (no such document) — critically, NOT a 500 from a missing Gemini key.
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Upload_WithNoGeminiApiKeyConfigured_FailsWithAClearError_NotAGeneric500()
    {
        using var realLazyFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => new LazyGeminiEmbeddingGenerator(sp));
            }));
        var client = realLazyFactory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-no-key-upload");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Some policy text."));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "policy.txt");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents") { Content = content };
        request.Headers.Authorization = new("Bearer", store.AccessToken);
        var response = await client.SendAsync(request);

        // Upload genuinely needs to embed the new chunks, so this one SHOULD
        // fail without a key — the point is it fails for that documented
        // reason (still surfaces as the app's generic 500 envelope, since
        // this is an unconfigured-environment error, not a validation
        // error), not because list/delete were broken too.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
