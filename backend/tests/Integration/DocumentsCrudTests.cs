using System.Net;
using System.Net.Http.Json;
using System.Text;
using Api.Controllers;
using Application.Common;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// Real HTTP, real SQL Server, real chunking + SQL Server native vector
/// upsert — only the embedding call itself is faked (see
/// CustomWebApplicationFactory/FakeEmbeddingGenerator), so this exercises
/// the actual RAG storage pipeline without spending real Gemini tokens.
/// </summary>
[Collection("Integration")]
public class DocumentsCrudTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task UploadListDelete_RoundTrips()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-crud");

        var upload = await UploadTextDocument(client, store.AccessToken, "policy.txt", "Returns are accepted within 7 days with a receipt.");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.Equal("policy.txt", uploaded!.FileName);

        var list = await Authorized(client, HttpMethod.Get, "/api/v1/documents", store.AccessToken);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<PagedResult<DocumentDto>>();
        Assert.Contains(page!.Items, d => d.Id == uploaded.Id);

        var delete = await Authorized(client, HttpMethod.Delete, $"/api/v1/documents/{uploaded.Id}", store.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listAfterDelete = await Authorized(client, HttpMethod.Get, "/api/v1/documents", store.AccessToken);
        listAfterDelete.EnsureSuccessStatusCode();
        var pageAfterDelete = await listAfterDelete.Content.ReadFromJsonAsync<PagedResult<DocumentDto>>();
        Assert.DoesNotContain(pageAfterDelete!.Items, d => d.Id == uploaded.Id);
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedContentType()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-badtype");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "policy.pdf");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents") { Content = content };
        request.Headers.Authorization = new("Bearer", store.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Documents_AreTenantScoped()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-tenant-b");

        var upload = await UploadTextDocument(client, storeA.AccessToken, "internal.txt", "Store A's confidential supplier pricing.");
        upload.EnsureSuccessStatusCode();
        var uploaded = await upload.Content.ReadFromJsonAsync<DocumentDto>();

        var storeBList = await Authorized(client, HttpMethod.Get, "/api/v1/documents", storeB.AccessToken);
        storeBList.EnsureSuccessStatusCode();
        var page = await storeBList.Content.ReadFromJsonAsync<PagedResult<DocumentDto>>();
        Assert.DoesNotContain(page!.Items, d => d.Id == uploaded!.Id);

        // A 404, never a 403 — same information-leakage reasoning as
        // TenantIsolationTests for products.
        var crossTenantDelete = await Authorized(client, HttpMethod.Delete, $"/api/v1/documents/{uploaded!.Id}", storeB.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantDelete.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAiAssistantUsePermission_IsForbidden()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "doc-noperm");

        var staffEmail = $"staff-{Guid.NewGuid():N}@test.local";
        var create = await Authorized(client, HttpMethod.Post, "/api/v1/users", owner.AccessToken,
            body: new { email = staffEmail, password = AuthTestHelper.DefaultPassword, displayName = "No AI Access", permissions = new[] { Domain.Identity.Permissions.CatalogRead } });
        create.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug = owner.Slug, email = staffEmail, password = AuthTestHelper.DefaultPassword });
        loginResponse.EnsureSuccessStatusCode();
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        var response = await UploadTextDocument(client, tokens!.AccessToken, "x.txt", "irrelevant content");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadTextDocument(HttpClient client, string accessToken, string fileName, string text)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents") { Content = content };
        request.Headers.Authorization = new("Bearer", accessToken);
        return await client.SendAsync(request);
    }
}
