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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PublicSearchIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

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

    // ── /api/public/parse ────────────────────────────────────────────────────

    [Fact]
    public async Task PublicParse_WithValidQuery_ReturnsSummaryAndCandidateGroups()
    {
        var response = await _client.PostAsJsonAsync("/api/public/parse", new { Query = "duct tape" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("summary").GetString()));

        var groups = body.GetProperty("candidateGroups");
        Assert.True(groups.GetArrayLength() > 0);

        var firstGroup = groups[0];
        Assert.False(string.IsNullOrEmpty(firstGroup.GetProperty("searchTerm").GetString()));
        Assert.True(firstGroup.GetProperty("candidates").GetArrayLength() > 0);
    }

    [Fact]
    public async Task PublicParse_WithMissingQuery_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/public/parse", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── /api/public/search ───────────────────────────────────────────────────

    [Fact]
    public async Task PublicSearch_WithSelectedCandidates_ReturnsReportId()
    {
        // Step 1: parse
        var parseResponse = await _client.PostAsJsonAsync("/api/public/parse", new { Query = "duct tape" });
        Assert.Equal(HttpStatusCode.OK, parseResponse.StatusCode);

        var parseBody = await parseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var candidatesJson = parseBody.GetProperty("candidateGroups")[0].GetProperty("candidates");
        var candidate = JsonSerializer.Deserialize<ProductCandidate>(candidatesJson[0].GetRawText(), JsonOptions)!;

        // Step 2: search with the actual candidate from parse
        var searchResponse = await _client.PostAsJsonAsync("/api/public/search", new
        {
            query = "duct tape",
            selectedCandidates = new[] { candidate }
        });

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var searchBody = await searchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(searchBody.GetProperty("reportId").GetString()));
    }

    [Fact]
    public async Task PublicSearch_WithoutSelectedCandidates_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/public/search", new { query = "duct tape", selectedCandidates = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicSearch_WithMissingQuery_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/public/search", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── /api/public/report/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task GetPublicReport_EndToEnd_ReturnsAnonymousReport()
    {
        // Parse
        var parseResponse = await _client.PostAsJsonAsync("/api/public/parse", new { Query = "hammer" });
        Assert.Equal(HttpStatusCode.OK, parseResponse.StatusCode);

        var parseBody = await parseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var candidate = JsonSerializer.Deserialize<ProductCandidate>(
            parseBody.GetProperty("candidateGroups")[0].GetProperty("candidates")[0].GetRawText(), JsonOptions)!;

        // Search
        var searchResponse = await _client.PostAsJsonAsync("/api/public/search", new
        {
            query = "hammer",
            selectedCandidates = new[] { candidate }
        });
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var reportId = (await searchResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("reportId").GetString()!;

        // Fetch report
        var reportResponse = await _client.GetAsync($"/api/public/report/{reportId}");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var report = await reportResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(reportId, report.GetProperty("id").GetString());
        Assert.True(report.GetProperty("productComparisons").GetArrayLength() > 0);
    }

    // ── /api/public/search/{id}/status ──────────────────────────────────────

    [Fact]
    public async Task GetPublicSearchStatus_ForAnonymousSearch_ReturnsStatus()
    {
        var seeded = await _factory.SearchRepository.CreateAsync(new SearchRequest
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "anonymous",
            Status = SearchStatus.Queued
        });

        var response = await _client.GetAsync($"/api/public/search/{seeded.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(seeded.Id, body.GetProperty("id").GetString());
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
