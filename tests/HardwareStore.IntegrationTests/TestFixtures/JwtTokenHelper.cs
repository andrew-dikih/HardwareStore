using HardwareStore.Core.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HardwareStore.IntegrationTests.TestFixtures;

public static class JwtTokenHelper
{
    // These must match the defaults used in Program.cs / appsettings.json
    private const string Key = "your-super-secret-key-change-in-production-min-32-chars";
    private const string Issuer = "HardwareStore";
    private const string Audience = "HardwareStoreUsers";

    public static string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("status", user.Status.ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static User CreateActiveUser(
        string id = "user1",
        string email = "user@test.com",
        UserRole role = UserRole.User) => new()
    {
        Id = id,
        Email = email,
        DisplayName = "Test User",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
        Role = role,
        Status = UserStatus.Active
    };

    public static User CreateAdminUser(
        string id = "admin1",
        string email = "admin@test.com") => new()
    {
        Id = id,
        Email = email,
        DisplayName = "Admin User",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPass123!"),
        Role = UserRole.Admin,
        Status = UserStatus.Active
    };
}
