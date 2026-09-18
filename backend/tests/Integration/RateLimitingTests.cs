using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Integration;

/// <summary>
/// The shared CustomWebApplicationFactory runs with a deliberately huge
/// rate limit (docs/testing/testing-strategy.md — the whole test run
/// makes far more than 10 auth requests/minute, and that's not abuse).
/// This test layers a tiny limit on top via WithWebHostBuilder, which
/// derives a new factory from the same already-running container instead
/// of spinning up a second one — proving 429 actually triggers without
/// slowing down every other test in the suite.
/// </summary>
[Collection("Integration")]
public class RateLimitingTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ExceedingAuthRateLimit_Returns429()
    {
        using var lowLimitFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RATE_LIMIT_AUTH_PERMIT_LIMIT"] = "2",
                ["RATE_LIMIT_AUTH_WINDOW_SECONDS"] = "60"
            })));
        var client = lowLimitFactory.CreateClient();

        var body = new { storeSlug = "no-such-store", email = "nobody@test.local", password = "wrong" };

        var first = await client.PostAsJsonAsync("/api/v1/auth/login", body);
        var second = await client.PostAsJsonAsync("/api/v1/auth/login", body);
        var third = await client.PostAsJsonAsync("/api/v1/auth/login", body);

        // The first two consume the limit (both fail for a normal reason —
        // the store doesn't exist — but still count against the window);
        // the third is rejected by the limiter itself, before ever
        // reaching the controller.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
