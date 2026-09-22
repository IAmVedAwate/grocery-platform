using System.Net;
using System.Net.Http.Json;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// No email/SMTP integration exists (docs/checkpoints/skills-inventory.md),
/// so the reset token is only ever exposed in the HTTP response when the
/// host is running in Development — which is what WebApplicationFactory
/// runs as by default, and is exactly why this flow is completable in a
/// test at all. See AuthController.ForgotPassword's own remarks.
/// </summary>
[Collection("Integration")]
public class PasswordResetTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ForgotPassword_ThenResetPassword_AllowsLoginWithTheNewPassword_NotTheOld()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "reset-happy");

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { storeSlug = store.Slug, email = store.Email });
        forgot.EnsureSuccessStatusCode();
        var forgotBody = await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponseLike>();
        Assert.NotNull(forgotBody!.ResetToken);

        const string newPassword = "N3w!Password123";
        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = forgotBody.ResetToken, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var loginOld = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug = store.Slug, email = store.Email, password = AuthTestHelper.DefaultPassword });
        Assert.Equal(HttpStatusCode.NotFound, loginOld.StatusCode); // generic — same as any other bad-credentials response

        var loginNew = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug = store.Slug, email = store.Email, password = newPassword });
        loginNew.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ForgotPassword_ForAnUnknownEmail_RespondsIdenticallyToAKnownOne()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "reset-unknown");

        var knownResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { storeSlug = store.Slug, email = store.Email });
        var unknownResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { storeSlug = store.Slug, email = "nobody-here@test.local" });

        // Same status code, same response shape (a nullable resetToken
        // field either way) — the only difference is the *value*, which a
        // real client never sees compared side-by-side like this test does.
        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        var unknownBody = await unknownResponse.Content.ReadFromJsonAsync<ForgotPasswordResponseLike>();
        Assert.Null(unknownBody!.ResetToken);
    }

    [Fact]
    public async Task ResetPassword_WithATamperedToken_IsRejected()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "reset-tampered");

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { storeSlug = store.Slug, email = store.Email });
        var forgotBody = await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponseLike>();
        var tampered = forgotBody!.ResetToken![..^1] + "x";

        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = tampered, newPassword = "Whatever123!" });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    private sealed record ForgotPasswordResponseLike(string? ResetToken);
}
