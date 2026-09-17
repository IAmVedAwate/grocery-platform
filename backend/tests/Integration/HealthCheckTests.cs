using Xunit;

namespace Integration;

[Collection("Integration")]
public class HealthCheckTests(CustomWebApplicationFactory factory)
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
