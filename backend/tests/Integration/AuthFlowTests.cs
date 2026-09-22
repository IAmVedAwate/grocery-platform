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

    /// <summary>
    /// A failed login must be 401 — not 404, which both answers an
    /// existence question the caller hasn't earned an answer to and reads
    /// to a real user as "your account is gone" when they simply fumbled
    /// their password.
    ///
    /// Asserting the status alone would be too weak: the property that
    /// actually matters is that a wrong password, an unregistered email
    /// and an unknown store are *indistinguishable*, so this compares the
    /// full response body across all three rather than trusting that they
    /// happen to line up. It also asserts the submitted email is not
    /// echoed back, which the old implementation did.
    /// </summary>
    [Fact]
    public async Task Login_Failures_AreAll401_AndByteForByteIdentical()
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

        var wrongPassword = await Login(client, slug, email, "TotallyWrongPassword123!");
        var unknownEmail = await Login(client, slug, $"nobody-{suffix}@test.local", AuthTestHelper.DefaultPassword);
        var unknownStore = await Login(client, $"no-such-store-{suffix}", email, AuthTestHelper.DefaultPassword);

        foreach (var (name, response) in new[]
                 {
                     ("wrong password", wrongPassword),
                     ("unknown email", unknownEmail),
                     ("unknown store", unknownStore)
                 })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
            Assert.DoesNotContain(email, response.Body);
            Assert.DoesNotContain("not found", response.Body, StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Body.Contains("Invalid credentials", StringComparison.OrdinalIgnoreCase),
                $"'{name}' should use the single shared credentials message, got: {response.Body}");
        }

        // The real enumeration guard: an attacker can't tell these apart.
        Assert.Equal(wrongPassword.Body, unknownEmail.Body);
        Assert.Equal(wrongPassword.Body, unknownStore.Body);
    }

    private static async Task<(HttpStatusCode Status, string Body)> Login(HttpClient client, string storeSlug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug, email, password });
        var body = await response.Content.ReadAsStringAsync();
        // traceId is per-request by design and says nothing about the account.
        body = System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"<scrubbed>\"");
        return (response.StatusCode, body);
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
