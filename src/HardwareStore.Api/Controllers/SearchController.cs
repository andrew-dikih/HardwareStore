namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Core.Services;
using HardwareStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRetailerRepository _retailerRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly INaturalLanguageService _nlService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IUserRepository userRepository,
        IRetailerRepository retailerRepository,
        ISearchRepository searchRepository,
        INaturalLanguageService nlService,
        IRateLimitService rateLimitService,
        ILogger<SearchController> logger)
    {
        _userRepository = userRepository;
        _retailerRepository = retailerRepository;
        _searchRepository = searchRepository;
        _nlService = nlService;
        _rateLimitService = rateLimitService;
        _logger = logger;
    }

    [HttpPost("parse")]
    public async Task<IActionResult> ParseQuery([FromBody] ParseQueryRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _nlService.ParseSearchQueryAsync(request.Query);
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await _userRepository.GetByIdAsync(userId);
            var allRetailers = await _retailerRepository.GetEnabledAsync();
            
            var availableRetailers = user?.Role == UserRole.Admin
                ? allRetailers
                : allRetailers.Where(r => r.IsAvailableToAll || 
                    (user?.AllowedRetailerIds.Contains(r.Id) ?? false)).ToList();

            return Ok(new ParseQueryResponse
            {
                Summary = result.Summary,
                SuggestedProducts = result.SuggestedProducts,
                AdditionalItems = result.AdditionalItems,
                AvailableRetailers = availableRetailers.Select(r => new RetailerDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    LogoUrl = r.LogoUrl,
                    IsSelected = r.IsAvailableToAll
                }).ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSearch([FromBody] CreateSearchRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return Unauthorized();

        if (!await _rateLimitService.CheckSearchLimitAsync(user))
        {
            return StatusCode(429, new { 
                message = $"Daily search limit of {user.DailySearchLimit} reached. Resets at midnight UTC." 
            });
        }

        var allRetailers = await _retailerRepository.GetEnabledAsync();
        var allowedRetailerIds = user.Role == UserRole.Admin
            ? allRetailers.Select(r => r.Id).ToList()
            : allRetailers
                .Where(r => r.IsAvailableToAll || user.AllowedRetailerIds.Contains(r.Id))
                .Select(r => r.Id).ToList();

        var selectedRetailerIds = request.SelectedRetailerIds
            .Where(id => allowedRetailerIds.Contains(id))
            .ToList();

        if (!selectedRetailerIds.Any())
            return BadRequest(new { message = "No valid retailers selected." });

        // Standard users without explicit retailer permissions can only compare up to 2 retailers.
        // Users with AllowedRetailerIds can compare any retailer they've been granted access to.
        if (selectedRetailerIds.Count > 2 && user.Role != UserRole.Admin && !user.AllowedRetailerIds.Any())
            return Forbid();

        var searchRequest = new SearchRequest
        {
            UserId = userId,
            NaturalLanguageQuery = request.NaturalLanguageQuery,
            SelectedProducts = request.SelectedProducts,
            AdditionalItems = request.AdditionalItems,
            SelectedRetailerIds = selectedRetailerIds,
            Status = SearchStatus.Queued
        };

        var saved = await _searchRepository.CreateAsync(searchRequest);
        await _rateLimitService.IncrementSearchCountAsync(user);

        SearchBackgroundService.EnqueueSearch(saved.Id);

        return Ok(new { searchRequestId = saved.Id, status = "Queued" });
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetSearchStatus(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var search = await _searchRepository.GetByIdAsync(id);
        
        if (search == null) return NotFound();
        if (search.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        return Ok(new
        {
            id = search.Id,
            status = search.Status.ToString(),
            reportId = search.ReportId,
            errorMessage = search.ErrorMessage,
            createdAt = search.CreatedAt,
            completedAt = search.CompletedAt
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var searches = await _searchRepository.GetByUserIdAsync(userId);
        return Ok(searches.Select(s => new
        {
            id = s.Id,
            query = s.NaturalLanguageQuery,
            status = s.Status.ToString(),
            reportId = s.ReportId,
            createdAt = s.CreatedAt,
            completedAt = s.CompletedAt
        }));
    }
}

public record ParseQueryRequest(
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    string Query);

public class ParseQueryResponse
{
    public string Summary { get; set; } = string.Empty;
    public List<ProductSelection> SuggestedProducts { get; set; } = new();
    public List<ProductSelection> AdditionalItems { get; set; } = new();
    public List<RetailerDto> AvailableRetailers { get; set; } = new();
}

public class RetailerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsSelected { get; set; }
}

public class CreateSearchRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string NaturalLanguageQuery { get; set; } = string.Empty;
    public List<ProductSelection> SelectedProducts { get; set; } = new();
    public List<ProductSelection> AdditionalItems { get; set; } = new();
    [System.ComponentModel.DataAnnotations.Required]
    public List<string> SelectedRetailerIds { get; set; } = new();
}
