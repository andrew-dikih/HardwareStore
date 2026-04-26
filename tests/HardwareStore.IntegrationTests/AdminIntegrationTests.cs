using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class AdminIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<User> SeedAdminAsync()
    {
        var admin = JwtTokenHelper.CreateAdminUser(
            id: Guid.NewGuid().ToString(),
            email: $"admin-{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(admin);
        return admin;
    }

    // ─── User management ─────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsAllUsers()
    {
        var admin = await SeedAdminAsync();
        var client = CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetUsers_AsRegularUser_ReturnsForbidden()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(user);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingUsers_AsAdmin_ReturnsPendingList()
    {
        var admin = await SeedAdminAsync();

        var pendingUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"pending-{Guid.NewGuid()}@test.com",
            DisplayName = "Pending User",
            Status = UserStatus.PendingApproval
        };
        await _factory.UserRepository.CreateAsync(pendingUser);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.GetAsync("/api/admin/users/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ApproveUser_AsAdmin_ReturnsUpdatedUser()
    {
        var admin = await SeedAdminAsync();

        var pendingUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"approve-{Guid.NewGuid()}@test.com",
            DisplayName = "To Approve",
            Status = UserStatus.PendingApproval
        };
        await _factory.UserRepository.CreateAsync(pendingUser);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.PutAsJsonAsync($"/api/admin/users/{pendingUser.Id}/approve", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ApproveUser_NotFound_Returns404()
    {
        var admin = await SeedAdminAsync();
        var client = CreateAuthenticatedClient(admin);

        var response = await client.PutAsJsonAsync("/api/admin/users/does-not-exist/approve", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectUser_AsAdmin_SuspendsUser()
    {
        var admin = await SeedAdminAsync();

        var pendingUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"reject-{Guid.NewGuid()}@test.com",
            DisplayName = "To Reject",
            Status = UserStatus.PendingApproval
        };
        await _factory.UserRepository.CreateAsync(pendingUser);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.PutAsJsonAsync($"/api/admin/users/{pendingUser.Id}/reject", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Suspended", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateUserSettings_AsAdmin_UpdatesLimitAndRetailers()
    {
        var admin = await SeedAdminAsync();

        var targetUser = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(targetUser);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.PutAsJsonAsync($"/api/admin/users/{targetUser.Id}/settings", new
        {
            dailySearchLimit = 50,
            allowedRetailerIds = new[] { "homedepot" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, body.GetProperty("dailySearchLimit").GetInt32());
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_ReturnsNoContent()
    {
        var admin = await SeedAdminAsync();

        var userToDelete = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: $"{Guid.NewGuid()}@test.com");
        await _factory.UserRepository.CreateAsync(userToDelete);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.DeleteAsync($"/api/admin/users/{userToDelete.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ─── Retailer management ──────────────────────────────────────────────

    [Fact]
    public async Task GetRetailers_AsAdmin_ReturnsAll()
    {
        var admin = await SeedAdminAsync();

        await _factory.RetailerRepository.CreateAsync(new Retailer
        {
            Id = $"r-{Guid.NewGuid()}",
            Name = "Test Store",
            IsEnabled = true
        });

        var client = CreateAuthenticatedClient(admin);
        var response = await client.GetAsync("/api/admin/retailers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task CreateRetailer_AsAdmin_ReturnsCreated()
    {
        var admin = await SeedAdminAsync();
        var client = CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/retailers", new
        {
            name = "New Store",
            baseUrl = "https://newstore.example.com",
            logoUrl = "/logos/new.png",
            isEnabled = true,
            isAvailableToAll = false,
            scraperType = "Custom"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New Store", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateRetailer_AsAdmin_ReturnsUpdatedRetailer()
    {
        var admin = await SeedAdminAsync();
        var retailer = new Retailer
        {
            Id = $"r-{Guid.NewGuid()}",
            Name = "Old Name",
            BaseUrl = "https://old.example.com",
            IsEnabled = true
        };
        await _factory.RetailerRepository.CreateAsync(retailer);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.PutAsJsonAsync($"/api/admin/retailers/{retailer.Id}", new
        {
            name = "Updated Name",
            baseUrl = "https://updated.example.com",
            isEnabled = true,
            isAvailableToAll = true,
            scraperType = "Updated"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteRetailer_AsAdmin_ReturnsNoContent()
    {
        var admin = await SeedAdminAsync();
        var retailer = new Retailer
        {
            Id = $"r-{Guid.NewGuid()}",
            Name = "To Delete",
            IsEnabled = true
        };
        await _factory.RetailerRepository.CreateAsync(retailer);

        var client = CreateAuthenticatedClient(admin);
        var response = await client.DeleteAsync($"/api/admin/retailers/{retailer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
