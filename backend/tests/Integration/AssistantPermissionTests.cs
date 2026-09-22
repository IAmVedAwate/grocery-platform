using System.Net;
using System.Net.Http.Json;
using Domain.Identity;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// [RequirePermission] is an authorization POLICY (RequirePermissionAttribute.cs)
/// evaluated by ASP.NET Core's authorization middleware BEFORE the
/// controller — and therefore IAiAssistantService/IChatClient — is ever
/// activated. That's what makes this test possible with no GEMINI_API_KEY
/// configured at all: a rejected request never reaches Gemini or even
/// constructs the client wrapping it. The "assistant actually answers a
/// question" path is deliberately NOT automated here — see AiTools.cs and
/// docs/testing/testing-strategy.md; it's the one manual, minimal-token
/// live smoke test instead.
/// </summary>
[Collection("Integration")]
public class AssistantPermissionTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Ask_WithoutAiAssistantUsePermission_IsForbidden_AndNeverTouchesGemini()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "assistant-noperm");

        var staffEmail = $"staff-{Guid.NewGuid():N}@test.local";
        var create = await Authorized(client, HttpMethod.Post, "/api/v1/users", owner.AccessToken,
            body: new { email = staffEmail, password = AuthTestHelper.DefaultPassword, displayName = "No AI Access", permissions = new[] { Permissions.CatalogRead } });
        create.EnsureSuccessStatusCode();

        var staffToken = await LoginAsync(client, owner.Slug, staffEmail, AuthTestHelper.DefaultPassword);

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/assistant/ask", staffToken,
            body: new { question = "What's low on stock?" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ask_WithoutAuthentication_IsUnauthorized()
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/assistant/ask")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { question = "What's low on stock?" })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string storeSlug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug, email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponseLike>();
        return body!.AccessToken;
    }

    private sealed record AccessTokenResponseLike(string AccessToken);
}
