using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signup_WithValidData_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "newuser@example.com",
            displayName = "New User",
            password = "SecurePass1!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("approval", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signup_WithDuplicateEmail_ReturnsConflict()
    {
        var existing = JwtTokenHelper.CreateActiveUser(email: "duplicate@example.com");
        await _factory.UserRepository.CreateAsync(existing);

        var response = await _client.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "duplicate@example.com",
            displayName = "Dup User",
            password = "SecurePass1!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Signup_WithMissingFields_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "notvalid"
            // missing displayName, password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUserInfo()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: "logintest@example.com");
        await _factory.UserRepository.CreateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "logintest@example.com",
            password = "TestPass123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("logintest@example.com", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var user = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: "wrongpw@example.com");
        await _factory.UserRepository.CreateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "wrongpw@example.com",
            password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithPendingApprovalUser_ReturnsUnauthorized()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "pending@example.com",
            DisplayName = "Pending",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
            Status = UserStatus.PendingApproval
        };
        await _factory.UserRepository.CreateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "pending@example.com",
            password = "TestPass123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("pending", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithSuspendedUser_ReturnsUnauthorized()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "suspended@example.com",
            DisplayName = "Suspended",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
            Status = UserStatus.Suspended
        };
        await _factory.UserRepository.CreateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "suspended@example.com",
            password = "TestPass123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
