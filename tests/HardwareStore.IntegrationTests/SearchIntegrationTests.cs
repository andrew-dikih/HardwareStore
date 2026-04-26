using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class SearchIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SearchIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(User user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(user));
        return client;
    }

    private User SeedActiveUser()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        _factory.UserRepository.CreateAsync(user).GetAwaiter().GetResult();

        // Seed retailers so the parse/create endpoints can build the retailer list
        _factory.RetailerRepository.CreateAsync(new Retailer
        {
            Id = "homedepot",
            Name = "Home Depot",
            IsEnabled = true,
            IsAvailableToAll = true
        }).GetAwaiter().GetResult();

        _factory.RetailerRepository.CreateAsync(new Retailer
        {
            Id = "lowes",
            Name = "Lowe's",
            IsEnabled = true,
            IsAvailableToAll = true
        }).GetAwaiter().GetResult();

        return user;
    }

    [Fact]
    public async Task ParseQuery_Authenticated_ReturnsProductsAndRetailers()
    {
        var user = SeedActiveUser();
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/search/parse", new { query = "2x4 lumber" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("suggestedProducts", out var products));
        Assert.True(products.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ParseQuery_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/search/parse", new { query = "screws" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSearch_HappyPath_ReturnsSearchId()
    {
        var user = SeedActiveUser();
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/search", new
        {
            naturalLanguageQuery = "paint brush",
            selectedProducts = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString(),
                    name = "Paint Brush",
                    searchTerm = "paint brush",
                    isSelected = true
                }
            },
            additionalItems = Array.Empty<object>(),
            selectedRetailerIds = new[] { "homedepot", "lowes" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("searchRequestId").GetString()));
        Assert.Equal("Queued", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateSearch_WithNoValidRetailers_ReturnsBadRequest()
    {
        var user = SeedActiveUser();
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/search", new
        {
            naturalLanguageQuery = "screws",
            selectedProducts = Array.Empty<object>(),
            additionalItems = Array.Empty<object>(),
            selectedRetailerIds = new[] { "nonexistent-retailer" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSearchStatus_ForOwnSearch_ReturnsStatus()
    {
        var user = SeedActiveUser();

        var searchRequest = new SearchRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            NaturalLanguageQuery = "drill",
            Status = SearchStatus.Queued
        };
        await _factory.SearchRepository.CreateAsync(searchRequest);

        var client = CreateAuthenticatedClient(user);
        var response = await client.GetAsync($"/api/search/{searchRequest.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(searchRequest.Id, body.GetProperty("id").GetString());
        Assert.Equal("Queued", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetSearchStatus_ForOtherUsersSearch_ReturnsForbid()
    {
        var owner = SeedActiveUser();
        var otherUser = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(otherUser);

        var searchRequest = new SearchRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserId = owner.Id,
            Status = SearchStatus.Queued
        };
        await _factory.SearchRepository.CreateAsync(searchRequest);

        var client = CreateAuthenticatedClient(otherUser);
        var response = await client.GetAsync($"/api/search/{searchRequest.Id}/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSearchStatus_NotFound_Returns404()
    {
        var user = SeedActiveUser();
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/search/nonexistent-id/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSearchHistory_ReturnsUserSearches()
    {
        var user = SeedActiveUser();

        await _factory.SearchRepository.CreateAsync(new SearchRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            NaturalLanguageQuery = "nails",
            Status = SearchStatus.Completed
        });

        var client = CreateAuthenticatedClient(user);
        var response = await client.GetAsync("/api/search/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }
}
