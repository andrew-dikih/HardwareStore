using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;

namespace HardwareStore.IntegrationTests;

/// <summary>
/// Smoke test – verifies the application starts correctly with in-memory infrastructure.
/// </summary>
public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task App_StartsSuccessfully_AndRespondsToRequests()
    {
        var client = _factory.CreateClient();

        // Any endpoint will do; 401 proves the API started and auth is wired up.
        var response = await client.GetAsync("/api/reports");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status: {response.StatusCode}");
    }
}
