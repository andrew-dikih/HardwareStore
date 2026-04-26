using HardwareStore.Core.Models;
using HardwareStore.IntegrationTests.TestFixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HardwareStore.IntegrationTests;

public class FacebookAuthIntegrationTests : IClassFixture<FacebookWebApplicationFactory>
{
    private readonly FacebookWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FacebookAuthIntegrationTests(FacebookWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task FacebookInitiate_RedirectsToFacebook()
    {
        var response = await _client.GetAsync("/api/auth/facebook");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("facebook.com", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FacebookCallback_WithNewUser_CreatesActiveUserAndReturnsToken()
    {
        var facebookId = "fb-new-user-123";
        var email = "fb-newuser@example.com";
        var name = "Facebook User";

        FakeCookieAuthHandler.SetPrincipal(CreateFacebookPrincipal(facebookId, email, name));

        var response = await _client.GetAsync("/api/auth/facebook/callback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal(name, body.GetProperty("displayName").GetString());

        var createdUser = await _factory.UserRepository.GetByFacebookIdAsync(facebookId);
        Assert.NotNull(createdUser);
        Assert.Equal(UserStatus.Active, createdUser.Status);
        Assert.Equal(facebookId, createdUser.FacebookId);
    }

    [Fact]
    public async Task FacebookCallback_WithExistingFacebookUser_ReturnsToken()
    {
        var facebookId = "fb-existing-456";
        var existingUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "fb-existing@example.com",
            DisplayName = "Existing FB User",
            FacebookId = facebookId,
            Status = UserStatus.Active
        };
        await _factory.UserRepository.CreateAsync(existingUser);

        FakeCookieAuthHandler.SetPrincipal(
            CreateFacebookPrincipal(facebookId, existingUser.Email, existingUser.DisplayName));

        var response = await _client.GetAsync("/api/auth/facebook/callback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal(existingUser.Email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task FacebookCallback_WhenEmailMatchesExistingUser_LinksFacebookId()
    {
        var facebookId = "fb-link-789";
        var existingUser = JwtTokenHelper.CreateActiveUser(
            id: Guid.NewGuid().ToString(),
            email: "fb-link@example.com");
        await _factory.UserRepository.CreateAsync(existingUser);

        FakeCookieAuthHandler.SetPrincipal(
            CreateFacebookPrincipal(facebookId, existingUser.Email, existingUser.DisplayName));

        var response = await _client.GetAsync("/api/auth/facebook/callback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await _factory.UserRepository.GetByEmailAsync(existingUser.Email);
        Assert.NotNull(updatedUser);
        Assert.Equal(facebookId, updatedUser.FacebookId);
    }

    [Fact]
    public async Task FacebookCallback_WithSuspendedUser_ReturnsUnauthorized()
    {
        var facebookId = "fb-suspended-101";
        var suspendedUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "fb-suspended@example.com",
            DisplayName = "Suspended",
            FacebookId = facebookId,
            Status = UserStatus.Suspended
        };
        await _factory.UserRepository.CreateAsync(suspendedUser);

        FakeCookieAuthHandler.SetPrincipal(
            CreateFacebookPrincipal(facebookId, suspendedUser.Email, suspendedUser.DisplayName));

        var response = await _client.GetAsync("/api/auth/facebook/callback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FacebookCallback_WhenAuthenticationFails_ReturnsBadRequest()
    {
        FakeCookieAuthHandler.SetPrincipal(null);

        var response = await _client.GetAsync("/api/auth/facebook/callback");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static ClaimsPrincipal CreateFacebookPrincipal(string facebookId, string email, string name)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, facebookId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

/// <summary>
/// A web application factory that replaces cookie authentication with a fake handler
/// so the Facebook callback can be tested without a real browser flow.
/// Provides dummy Facebook credentials so the Facebook auth scheme is registered.
/// </summary>
public class FacebookWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Provide dummy Facebook credentials so the scheme is registered
        builder.UseSetting("Facebook:AppId", "test-app-id");
        builder.UseSetting("Facebook:AppSecret", "test-app-secret");

        builder.ConfigureServices(services =>
        {
            // Replace the Cookie auth handler so that AuthenticateAsync returns our fake principal
            services.Configure<AuthenticationOptions>(options =>
            {
                var cookieScheme = options.Schemes
                    .FirstOrDefault(s => s.Name == CookieAuthenticationDefaults.AuthenticationScheme);

                if (cookieScheme != null)
                    cookieScheme.HandlerType = typeof(FakeCookieAuthHandler);
                else
                    options.AddScheme<FakeCookieAuthHandler>(
                        CookieAuthenticationDefaults.AuthenticationScheme, displayName: null);
            });
        });
    }
}

public class FakeCookieAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationSignOutHandler
{
    private static volatile ClaimsPrincipal? _principal;

    public static void SetPrincipal(ClaimsPrincipal? principal) => _principal = principal;

    public FakeCookieAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_principal == null)
            return Task.FromResult(AuthenticateResult.Fail("No principal configured for fake cookie auth."));

        var ticket = new AuthenticationTicket(_principal, CookieAuthenticationDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public Task SignOutAsync(AuthenticationProperties? properties) => Task.CompletedTask;
}
