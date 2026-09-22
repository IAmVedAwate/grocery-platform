using System.Security.Claims;
using Application.Ai;
using Domain.Identity;
using Infrastructure.Ai;
using Integration.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Integration;

/// <summary>
/// The single most safety-critical property of the RAG pipeline
/// (docs/PRD.md §16): a document uploaded by one store must never surface
/// in another store's assistant search results. Drives AiTools.SearchDocumentsAsync
/// directly (bypassing the LLM/agent loop entirely — see AiTools.cs for why
/// that split makes this possible) against the REAL SQL Server vector
/// store and REAL VectorSearchOptions.Filter, with only the embedding call
/// itself faked (FakeEmbeddingGenerator) — same reasoning as
/// TenantIsolationTests.cs for relational data, applied to the vector
/// store.
/// </summary>
[Collection("Integration")]
public class SearchDocumentsTenantIsolationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task DocumentUploadedByOneStore_NeverAppearsInAnotherStores_SearchDocuments()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "rag-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "rag-tenant-b");

        await UploadTextDocument(client, storeA.AccessToken, "supplier-agreement.txt",
            "Our supplier agreement grants net-30 payment terms exclusively to Acme Wholesale.");

        // Store A can find its own document.
        var (storeAResult, storeACitations) = await SearchAsStore(storeA.StoreId, "payment terms");
        Assert.Contains("Acme Wholesale", storeAResult);
        Assert.Contains(storeACitations, c => c.FileName == "supplier-agreement.txt");

        // Store B — a different tenant that uploaded nothing — must see none of it.
        var (storeBResult, storeBCitations) = await SearchAsStore(storeB.StoreId, "payment terms");
        Assert.DoesNotContain("Acme Wholesale", storeBResult);
        Assert.Empty(storeBCitations);
    }

    private async Task<(string ResultText, List<DocumentCitation> Citations)> SearchAsStore(Guid storeId, string query)
    {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(Infrastructure.Identity.HttpTenantContext.StoreIdClaimType, storeId.ToString()),
                    new Claim("permission", Permissions.AiAssistantUse)
                ], "TestAuth"))
        };

        var aiTools = scope.ServiceProvider.GetRequiredService<AiTools>();
        var citations = new List<DocumentCitation>();
        var result = await aiTools.SearchDocumentsAsync(query, citations, CancellationToken.None);
        return (result, citations);
    }

    private static async Task<HttpResponseMessage> UploadTextDocument(HttpClient client, string accessToken, string fileName, string text)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents") { Content = content };
        request.Headers.Authorization = new("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
