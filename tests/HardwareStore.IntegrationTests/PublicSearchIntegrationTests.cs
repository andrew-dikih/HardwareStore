using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class PublicSearchIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PublicSearchIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Seed retailers required by the public search endpoint
        factory.RetailerRepository.CreateAsync(new Retailer
        {
            Id = "homedepot",
            Name = "Home Depot",
            IsEnabled = true,
            IsAvailableToAll = true
        }).GetAwaiter().GetResult();

        factory.RetailerRepository.CreateAsync(new Retailer
        {
            Id = "lowes",
            Name = "Lowe's",
            IsEnabled = true,
            IsAvailableToAll = true
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task PublicSearch_WithValidQuery_ReturnsSearchIdAndProducts()
    {
        var response = await _client.PostAsJsonAsync("/api/public/search", new { query = "duct tape" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("searchRequestId").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("summary").GetString()));
        Assert.True(body.GetProperty("suggestedProducts").GetArrayLength() > 0);
    }

    [Fact]
    public async Task PublicSearch_WithMissingQuery_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/public/search", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicSearchStatus_ForAnonymousSearch_ReturnsStatus()
    {
        // First create a public search to get an ID
        var createResponse = await _client.PostAsJsonAsync("/api/public/search", new { query = "hammer" });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var searchId = createBody.GetProperty("searchRequestId").GetString()!;

        var statusResponse = await _client.GetAsync($"/api/public/search/{searchId}/status");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(searchId, statusBody.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetPublicSearchStatus_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/public/search/nonexistent/status");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicSearchStatus_ForNonAnonymousSearch_ReturnsForbid()
    {
        // Seed a search belonging to a real user (not anonymous)
        var userSearch = new SearchRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "some-real-user",
            Status = SearchStatus.Queued
        };
        await _factory.SearchRepository.CreateAsync(userSearch);

        var response = await _client.GetAsync($"/api/public/search/{userSearch.Id}/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
