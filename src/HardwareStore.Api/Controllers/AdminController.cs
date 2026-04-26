namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRetailerRepository _retailerRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IUserRepository userRepository, IRetailerRepository retailerRepository,
        IEmailService emailService, ILogger<AdminController> logger)
    {
        _userRepository = userRepository;
        _retailerRepository = retailerRepository;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(u => new UserDto(u)));
    }

    [HttpGet("users/pending")]
    public async Task<IActionResult> GetPendingUsers()
    {
        var users = await _userRepository.GetPendingApprovalsAsync();
        return Ok(users.Select(u => new UserDto(u)));
    }

    [HttpPut("users/{id}/approve")]
    public async Task<IActionResult> ApproveUser(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.Status = UserStatus.Active;
        user.ApprovedAt = DateTime.UtcNow;
        user.ApprovedBy = User.Identity?.Name;
        await _userRepository.UpdateAsync(user);

        try { await _emailService.SendApprovalNotificationAsync(user.Email, user.DisplayName); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send approval email"); }

        return Ok(new UserDto(user));
    }

    [HttpPut("users/{id}/reject")]
    public async Task<IActionResult> RejectUser(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.Status = UserStatus.Suspended;
        await _userRepository.UpdateAsync(user);

        try { await _emailService.SendRejectionNotificationAsync(user.Email, user.DisplayName); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send rejection email"); }

        return Ok(new UserDto(user));
    }

    [HttpPut("users/{id}/settings")]
    public async Task<IActionResult> UpdateUserSettings(string id, [FromBody] UpdateUserSettingsRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.DailySearchLimit = request.DailySearchLimit;
        user.AllowedRetailerIds = request.AllowedRetailerIds;
        if (request.Status.HasValue) user.Status = request.Status.Value;

        await _userRepository.UpdateAsync(user);
        return Ok(new UserDto(user));
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();
        await _userRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("retailers")]
    public async Task<IActionResult> GetRetailers()
    {
        var retailers = await _retailerRepository.GetAllAsync();
        return Ok(retailers);
    }

    [HttpPost("retailers")]
    public async Task<IActionResult> CreateRetailer([FromBody] CreateRetailerRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        var retailer = new Retailer
        {
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            LogoUrl = request.LogoUrl ?? string.Empty,
            IsEnabled = request.IsEnabled,
            IsAvailableToAll = request.IsAvailableToAll,
            ScraperType = request.ScraperType
        };

        var saved = await _retailerRepository.CreateAsync(retailer);
        return CreatedAtAction(nameof(GetRetailers), new { id = saved.Id }, saved);
    }

    [HttpPut("retailers/{id}")]
    public async Task<IActionResult> UpdateRetailer(string id, [FromBody] CreateRetailerRequest request)
    {
        var retailer = await _retailerRepository.GetByIdAsync(id);
        if (retailer == null) return NotFound();

        retailer.Name = request.Name;
        retailer.BaseUrl = request.BaseUrl;
        retailer.LogoUrl = request.LogoUrl ?? retailer.LogoUrl;
        retailer.IsEnabled = request.IsEnabled;
        retailer.IsAvailableToAll = request.IsAvailableToAll;
        retailer.ScraperType = request.ScraperType;

        await _retailerRepository.UpdateAsync(retailer);
        return Ok(retailer);
    }

    [HttpDelete("retailers/{id}")]
    public async Task<IActionResult> DeleteRetailer(string id)
    {
        var retailer = await _retailerRepository.GetByIdAsync(id);
        if (retailer == null) return NotFound();
        await _retailerRepository.DeleteAsync(id);
        return NoContent();
    }
}

public class UserDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int DailySearchLimit { get; set; }
    public int SearchesUsedToday { get; set; }
    public List<string> AllowedRetailerIds { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public UserDto(User user)
    {
        Id = user.Id;
        Email = user.Email;
        DisplayName = user.DisplayName;
        Role = user.Role.ToString();
        Status = user.Status.ToString();
        DailySearchLimit = user.DailySearchLimit;
        SearchesUsedToday = user.SearchesUsedToday;
        AllowedRetailerIds = user.AllowedRetailerIds;
        CreatedAt = user.CreatedAt;
        ApprovedAt = user.ApprovedAt;
    }
}

public class UpdateUserSettingsRequest
{
    public int DailySearchLimit { get; set; } = 10;
    public List<string> AllowedRetailerIds { get; set; } = new();
    public UserStatus? Status { get; set; }
}

public class CreateRetailerRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Name { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    public string BaseUrl { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsAvailableToAll { get; set; } = false;
    public string ScraperType { get; set; } = string.Empty;
}
