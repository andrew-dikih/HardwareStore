using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class ReportsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsIntegrationTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task GetReportHistory_ReturnsOwnReports()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(user);

        await _factory.ReportRepository.CreateAsync(new SearchReport
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            QuerySummary = "deck screws",
            RecommendedRetailerName = "Home Depot"
        });

        var client = CreateAuthenticatedClient(user);
        var response = await client.GetAsync("/api/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
        Assert.Equal("deck screws", body[0].GetProperty("querySummary").GetString());
    }

    [Fact]
    public async Task GetReportHistory_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/reports");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReportById_OwnReport_ReturnsReport()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(user);

        var report = new SearchReport
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            QuerySummary = "socket wrench"
        };
        await _factory.ReportRepository.CreateAsync(report);

        var client = CreateAuthenticatedClient(user);
        var response = await client.GetAsync($"/api/reports/{report.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(report.Id, body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetReportById_OtherUsersReport_ReturnsForbid()
    {
        var owner = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(owner);

        var otherUser = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(otherUser);

        var report = new SearchReport
        {
            Id = Guid.NewGuid().ToString(),
            UserId = owner.Id,
            QuerySummary = "private report"
        };
        await _factory.ReportRepository.CreateAsync(report);

        var client = CreateAuthenticatedClient(otherUser);
        var response = await client.GetAsync($"/api/reports/{report.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReportById_NotFound_Returns404()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(user);

        var client = CreateAuthenticatedClient(user);
        var response = await client.GetAsync("/api/reports/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReportById_AdminCanViewAnyReport()
    {
        var regularUser = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(regularUser);

        var admin = JwtTokenHelper.CreateAdminUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(admin);

        var report = new SearchReport
        {
            Id = Guid.NewGuid().ToString(),
            UserId = regularUser.Id,
            QuerySummary = "user's private report"
        };
        await _factory.ReportRepository.CreateAsync(report);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.GetAsync($"/api/reports/{report.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
