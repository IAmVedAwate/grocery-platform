using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration;

/// <summary>
/// Real integration test against the actual hosted API (WebApplicationFactory),
/// proving the integration test harness works end-to-end. Expand with
/// Testcontainers-backed SQL Server once Infrastructure/EF Core land in
/// Phase 1 (see docs/testing/testing-strategy.md).
/// </summary>
public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_ReturnsSuccess(string endpoint)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(endpoint);

        Assert.True(response.IsSuccessStatusCode);
    }
}
