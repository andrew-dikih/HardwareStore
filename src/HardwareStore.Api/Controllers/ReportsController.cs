namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserRepository _userRepository;

    public ReportsController(IReportRepository reportRepository, IUserRepository userRepository)
    {
        _reportRepository = reportRepository;
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var reports = await _reportRepository.GetByUserIdAsync(userId);
        return Ok(reports.Select(r => new
        {
            id = r.Id,
            querySummary = r.QuerySummary,
            recommendedRetailerName = r.RecommendedRetailerName,
            createdAt = r.CreatedAt,
            productCount = r.ProductComparisons.Count
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReport(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var report = await _reportRepository.GetByIdAsync(id);
        
        if (report == null) return NotFound();
        if (report.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        return Ok(report);
    }
}
