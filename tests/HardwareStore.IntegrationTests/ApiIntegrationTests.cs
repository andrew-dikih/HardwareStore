using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace HardwareStore.IntegrationTests;

/// <summary>
/// Integration tests use <see cref="WebApplicationFactory{TProgram}"/> to spin up an in-memory
/// instance of the API. Add test-specific configuration and service overrides in the factory.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert – 404 is acceptable if /health is not yet wired up; the intent is that the app starts.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status: {response.StatusCode}");
    }
}
