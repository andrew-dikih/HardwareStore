namespace HardwareStore.Api.Controllers;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

[ApiController]
[Route("api/public")]
public class PublicSearchController : ControllerBase
{
    private readonly IRetailerRepository _retailerRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly IReportRepository _reportRepository;
    private readonly INaturalLanguageService _nlService;
    private readonly IEnumerable<IRetailerSearchClient> _retailerClients;
    private readonly ILogger<PublicSearchController> _logger;

    public PublicSearchController(
        IRetailerRepository retailerRepository,
        ISearchRepository searchRepository,
        IReportRepository reportRepository,
        INaturalLanguageService nlService,
        IEnumerable<IRetailerSearchClient> retailerClients,
        ILogger<PublicSearchController> logger)
    {
        _retailerRepository = retailerRepository;
        _searchRepository = searchRepository;
        _reportRepository = reportRepository;
        _nlService = nlService;
        _retailerClients = retailerClients;
        _logger = logger;
    }

    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] PublicParseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var nlResult = await _nlService.ParseSearchQueryAsync(request.Query);

            var retailers = (await _retailerRepository.GetEnabledAsync())
                .Where(r => r.IsAvailableToAll && (r.Id == "homedepot" || r.Id == "lowes"))
                .ToList();

            var searchItems = nlResult.SuggestedProducts
                .Where(p => p.IsSelected)
                .Concat(nlResult.AdditionalItems)
                .Take(5)
                .ToList();

            var candidateGroups = new List<object>();

            foreach (var item in searchItems)
            {
                var allResults = new List<RetailerProductResult>();

                foreach (var retailer in retailers)
                {
                    var client = _retailerClients.FirstOrDefault(c => c.RetailerId == retailer.Id);
                    if (client == null) continue;

                    try
                    {
                        var results = await client.SearchProductAsync(item.SearchTerm, retailer);
                        allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error searching {Retailer} for '{Term}'", retailer.Name, item.SearchTerm);
                    }
                }

                var candidates = GroupIntoCandidates(allResults, item.SearchTerm);

                candidateGroups.Add(new
                {
                    searchTerm = item.SearchTerm,
                    displayName = item.Name,
                    isAdditional = item.IsAdditional,
                    candidates
                });
            }

            return Ok(new { summary = nlResult.Summary, candidateGroups });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> PublicSearch([FromBody] PublicSearchSubmitRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var selectedCandidates = request.SelectedCandidates
            .Where(c => c.Items.Count > 0)
            .Take(10)
            .ToList();

        if (selectedCandidates.Count == 0)
            return BadRequest(new { message = "Please select at least one product." });

        var report = new SearchReport
        {
            UserId = "anonymous",
            QuerySummary = request.Query
        };

        foreach (var candidate in selectedCandidates)
        {
            var comparison = new ProductComparison
            {
                ProductSelectionId = candidate.Id,
                ProductName = candidate.DisplayName,
                SearchTerm = candidate.SearchTerm
            };

            foreach (var item in candidate.Items.Take(5))
            {
                if (item.Price <= 0) continue;
                comparison.RetailerResults.Add(new RetailerProductResult
                {
                    RetailerId = item.RetailerId,
                    RetailerName = item.RetailerName,
                    ProductTitle = item.ProductTitle,
                    Price = item.Price,
                    PriceDisplay = item.PriceDisplay,
                    ProductUrl = item.ProductUrl ?? string.Empty,
                    ImageUrl = item.ImageUrl,
                    IsAvailable = true
                });
            }

            report.ProductComparisons.Add(comparison);
        }

        var retailerIds = selectedCandidates
            .SelectMany(c => c.Items.Select(i => i.RetailerId))
            .Distinct();

        foreach (var retailerId in retailerIds)
        {
            var retailerName = selectedCandidates
                .SelectMany(c => c.Items)
                .First(i => i.RetailerId == retailerId).RetailerName;

            var total = new RetailerTotal
            {
                RetailerId = retailerId,
                RetailerName = retailerName
            };

            foreach (var comparison in report.ProductComparisons)
            {
                var bestResult = comparison.RetailerResults
                    .Where(r => r.RetailerId == retailerId && r.IsAvailable)
                    .OrderBy(r => r.Price)
                    .FirstOrDefault();

                if (bestResult != null) { total.TotalPrice += bestResult.Price; total.ProductsFound++; }
                else total.ProductsNotFound++;
            }

            total.TotalPriceDisplay = total.ProductsFound > 0 ? $"${total.TotalPrice:F2}" : "N/A";
            report.RetailerTotals[retailerId] = total;
        }

        var best = report.RetailerTotals.Values
            .Where(t => t.ProductsNotFound == 0).OrderBy(t => t.TotalPrice).FirstOrDefault()
            ?? report.RetailerTotals.Values.OrderByDescending(t => t.ProductsFound).ThenBy(t => t.TotalPrice).FirstOrDefault();

        if (best != null)
        {
            report.RecommendedRetailerId = best.RetailerId;
            report.RecommendedRetailerName = best.RetailerName;
            report.RecommendationReason = best.ProductsNotFound == 0
                ? $"{best.RetailerName} has all products at the best total price of {best.TotalPriceDisplay}."
                : $"{best.RetailerName} has the most products available at {best.TotalPriceDisplay}.";
        }

        var saved = await _reportRepository.CreateAsync(report);
        return Ok(new { reportId = saved.Id });
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

    [HttpGet("report/{id}")]
    public async Task<IActionResult> GetPublicReport(string id)
    {
        var report = await _reportRepository.GetByIdAsync(id);
        if (report == null) return NotFound();
        if (report.UserId != "anonymous") return Forbid();
        return Ok(report);
    }

    // ── Candidate grouping ────────────────────────────────────────────────────

    private static List<ProductCandidate> GroupIntoCandidates(
        List<RetailerProductResult> results, string searchTerm)
    {
        var byRetailer = results
            .GroupBy(r => r.RetailerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (byRetailer.Count < 2)
        {
            return results.Select(r => new ProductCandidate
            {
                SearchTerm = searchTerm,
                DisplayName = r.ProductTitle,
                BrandNames = [ExtractBrand(r.ProductTitle)],
                Confidence = ProductCandidateConfidence.Individual,
                Items = [MapToItem(r)]
            }).ToList();
        }

        var retailerIds = byRetailer.Keys.ToList();
        var listA = byRetailer[retailerIds[0]];
        var listB = byRetailer[retailerIds[1]];

        var candidates = new List<ProductCandidate>();
        var usedB = new HashSet<int>();

        foreach (var productA in listA)
        {
            double bestSim = 0;
            int bestIdx = -1;

            for (int i = 0; i < listB.Count; i++)
            {
                if (usedB.Contains(i)) continue;
                double sim = JaccardSimilarity(productA.ProductTitle, listB[i].ProductTitle);
                if (sim > bestSim) { bestSim = sim; bestIdx = i; }
            }

            var candidate = new ProductCandidate { SearchTerm = searchTerm };
            candidate.Items.Add(MapToItem(productA));

            if (bestIdx >= 0 && bestSim >= 0.3)
            {
                usedB.Add(bestIdx);
                candidate.Items.Add(MapToItem(listB[bestIdx]));
                candidate.Confidence = bestSim >= 0.55
                    ? ProductCandidateConfidence.Exact
                    : ProductCandidateConfidence.SpecMatch;

                var brands = new[] { ExtractBrand(productA.ProductTitle), ExtractBrand(listB[bestIdx].ProductTitle) }
                    .Where(b => !string.IsNullOrEmpty(b)).Distinct().ToList();
                candidate.BrandNames = brands;
                candidate.DisplayName = candidate.Confidence == ProductCandidateConfidence.Exact
                    ? productA.ProductTitle
                    : $"{string.Join(" / ", brands)} — {searchTerm}";
            }
            else
            {
                candidate.Confidence = ProductCandidateConfidence.Individual;
                candidate.BrandNames = [ExtractBrand(productA.ProductTitle)];
                candidate.DisplayName = productA.ProductTitle;
            }

            candidates.Add(candidate);
        }

        for (int i = 0; i < listB.Count; i++)
        {
            if (!usedB.Contains(i))
                candidates.Add(new ProductCandidate
                {
                    SearchTerm = searchTerm,
                    DisplayName = listB[i].ProductTitle,
                    BrandNames = [ExtractBrand(listB[i].ProductTitle)],
                    Confidence = ProductCandidateConfidence.Individual,
                    Items = [MapToItem(listB[i])]
                });
        }

        return [.. candidates.OrderBy(c => (int)c.Confidence)];
    }

    private static double JaccardSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var setA = Tokenize(a);
        var setB = Tokenize(b);
        if (setA.Count == 0 || setB.Count == 0) return 0;
        return (double)setA.Intersect(setB).Count() / setA.Union(setB).Count();
    }

    private static readonly HashSet<string> Stopwords = new(
        ["the", "a", "an", "and", "or", "for", "with", "in", "of", "to", "at", "by", "is", "it"],
        StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new char[] { ' ', '-', '_', '/', '(', ')', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !Stopwords.Contains(t))
            .ToHashSet();

    private static string ExtractBrand(string title) =>
        title.Split(new char[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private static ProductCandidateItem MapToItem(RetailerProductResult r) => new()
    {
        RetailerId = r.RetailerId,
        RetailerName = r.RetailerName,
        ProductTitle = r.ProductTitle,
        Price = r.Price,
        PriceDisplay = r.PriceDisplay,
        ProductUrl = r.ProductUrl,
        ImageUrl = r.ImageUrl
    };
}

public record PublicParseRequest(
    [Required][MaxLength(500)] string Query);

public class PublicSearchSubmitRequest
{
    [Required][MaxLength(500)]
    public string Query { get; set; } = string.Empty;

    public List<ProductCandidate> SelectedCandidates { get; set; } = new();
}


