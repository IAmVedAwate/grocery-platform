using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Integration.Helpers;
using Xunit;

namespace Integration;

[Collection("Integration")]
public class AuthFlowTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task RegisterStore_ThenLogin_Succeeds()
    {
        var client = factory.CreateClient();

        var registered = await AuthTestHelper.RegisterAndLoginAsync(client, "auth-flow");

        Assert.NotEqual(Guid.Empty, registered.StoreId);
        Assert.False(string.IsNullOrWhiteSpace(registered.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"wrong-pw-{suffix}";
        var email = $"owner-{suffix}@test.local";

        await client.PostAsJsonAsync("/api/v1/auth/register-store", new
        {
            storeName = "Wrong PW Store",
            slug,
            adminEmail = email,
            adminPassword = AuthTestHelper.DefaultPassword,
            adminDisplayName = "Test Owner"
        });

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            storeSlug = slug,
            email,
            password = "TotallyWrongPassword123!"
        });

        // Deliberately the same generic "not found" as an unknown email —
        // see docs/security/security-model.md (don't reveal which part failed).
        Assert.Equal(HttpStatusCode.NotFound, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldCookieNoLongerWorks()
    {
        var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"refresh-{suffix}";
        var email = $"owner-{suffix}@test.local";

        await client.PostAsJsonAsync("/api/v1/auth/register-store", new
        {
            storeName = "Refresh Store",
            slug,
            adminEmail = email,
            adminPassword = AuthTestHelper.DefaultPassword,
            adminDisplayName = "Test Owner"
        });
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug = slug, email, password = AuthTestHelper.DefaultPassword });
        loginResponse.EnsureSuccessStatusCode();

        // First refresh: the httpOnly cookie set by login is sent automatically
        // by the cookie-handling HttpClient.
        var firstRefresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var firstTokens = await firstRefresh.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(firstTokens);

        // Second refresh with the NEW cookie succeeds and rotates again.
        var secondRefresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
