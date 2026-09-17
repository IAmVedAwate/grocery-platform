using System.Net.Http.Json;
using Api.Controllers;

namespace Integration.Helpers;

/// <summary>Shared setup for tests that need an authenticated store —
/// every call uses a Guid-suffixed slug/email so parallel tests sharing
/// one database container never collide.</summary>
public static class AuthTestHelper
{
    public const string DefaultPassword = "P@ssword123!";

    public static async Task<RegisteredStore> RegisterAndLoginAsync(HttpClient client, string storeNamePrefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"{storeNamePrefix}-{suffix}";
        var email = $"owner-{suffix}@test.local";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register-store", new
        {
            storeName = $"{storeNamePrefix} {suffix}",
            slug,
            adminEmail = email,
            adminPassword = DefaultPassword,
            adminDisplayName = "Test Owner"
        });
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterStoreResponse>();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            storeSlug = slug,
            email,
            password = DefaultPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        return new RegisteredStore(registered!.StoreId, slug, email, tokens!.AccessToken);
    }
}

public sealed record RegisteredStore(Guid StoreId, string Slug, string Email, string AccessToken);
