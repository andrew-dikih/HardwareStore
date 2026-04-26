namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository userRepository, IEmailService emailService,
        IConfiguration configuration, ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new { message = "An account with this email already exists." });

        var user = new User
        {
            Email = request.Email.ToLower().Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.HashPassword(request.Password),
            Role = UserRole.User,
            Status = UserStatus.PendingApproval
        };

        await _userRepository.CreateAsync(user);

        try
        {
            await _emailService.SendSignupNotificationAsync(user.Email, user.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send signup notification email");
        }

        return Ok(new { message = "Account created. Awaiting admin approval." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userRepository.GetByEmailAsync(request.Email.ToLower().Trim());
        if (user == null || !BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (user.Status == UserStatus.PendingApproval)
            return Unauthorized(new { message = "Your account is pending admin approval." });

        if (user.Status == UserStatus.Suspended)
            return Unauthorized(new { message = "Your account has been suspended." });

        var token = GenerateToken(user);
        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        });
    }

    private string GenerateToken(User user)
    {
        var key = _configuration["Jwt:Key"] ?? "your-super-secret-key-change-in-production-min-32-chars";
        var issuer = _configuration["Jwt:Issuer"] ?? "HardwareStore";
        var audience = _configuration["Jwt:Audience"] ?? "HardwareStoreUsers";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("status", user.Status.ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record SignupRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    string Email,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MinLength(2)]
    string DisplayName,
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MinLength(8)]
    string Password);

public record LoginRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    string Email,
    [property: System.ComponentModel.DataAnnotations.Required]
    string Password);

public record LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
