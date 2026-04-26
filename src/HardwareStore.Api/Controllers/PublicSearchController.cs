namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/public")]
public class PublicSearchController : ControllerBase
{
    private readonly IRetailerRepository _retailerRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly INaturalLanguageService _nlService;
    private readonly ILogger<PublicSearchController> _logger;

    public PublicSearchController(
        IRetailerRepository retailerRepository,
        ISearchRepository searchRepository,
        INaturalLanguageService nlService,
        ILogger<PublicSearchController> logger)
    {
        _retailerRepository = retailerRepository;
        _searchRepository = searchRepository;
        _nlService = nlService;
        _logger = logger;
    }

    [HttpPost("search")]
    public async Task<IActionResult> PublicSearch([FromBody] PublicSearchRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _nlService.ParseSearchQueryAsync(request.Query);

            var retailers = (await _retailerRepository.GetEnabledAsync())
                .Where(r => r.IsAvailableToAll && (r.Id == "homedepot" || r.Id == "lowes"))
                .ToList();

            var searchRequest = new SearchRequest
            {
                UserId = "anonymous",
                NaturalLanguageQuery = request.Query,
                SelectedProducts = result.SuggestedProducts,
                AdditionalItems = result.AdditionalItems,
                SelectedRetailerIds = retailers.Select(r => r.Id).ToList(),
                Status = SearchStatus.Queued
            };

            var saved = await _searchRepository.CreateAsync(searchRequest);
            SearchBackgroundService.EnqueueSearch(saved.Id);

            return Ok(new
            {
                searchRequestId = saved.Id,
                summary = result.Summary,
                suggestedProducts = result.SuggestedProducts,
                additionalItems = result.AdditionalItems,
                retailers = retailers.Select(r => new { r.Id, r.Name, r.LogoUrl })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("search/{id}/status")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var search = await _searchRepository.GetByIdAsync(id);
        if (search == null) return NotFound();
        if (search.UserId != "anonymous") return Forbid();

        return Ok(new
        {
            id = search.Id,
            status = search.Status.ToString(),
            reportId = search.ReportId,
            errorMessage = search.ErrorMessage
        });
    }
}

public record PublicSearchRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(500)]
    string Query);
