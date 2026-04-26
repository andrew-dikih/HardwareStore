namespace HardwareStore.Infrastructure.Services;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.Extensions.Logging;

public class SearchJobService : ISearchJobService
{
    private readonly ISearchRepository _searchRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IRetailerRepository _retailerRepository;
    private readonly IEnumerable<IRetailerSearchClient> _retailerClients;
    private readonly ILogger<SearchJobService> _logger;

    public SearchJobService(
        ISearchRepository searchRepository,
        IReportRepository reportRepository,
        IRetailerRepository retailerRepository,
        IEnumerable<IRetailerSearchClient> retailerClients,
        ILogger<SearchJobService> logger)
    {
        _searchRepository = searchRepository;
        _reportRepository = reportRepository;
        _retailerRepository = retailerRepository;
        _retailerClients = retailerClients;
        _logger = logger;
    }

    public Task EnqueueSearchAsync(string searchRequestId)
    {
        SearchBackgroundService.EnqueueSearch(searchRequestId);
        return Task.CompletedTask;
    }

    public async Task ProcessSearchAsync(string searchRequestId)
    {
        var searchRequest = await _searchRepository.GetByIdAsync(searchRequestId);
        if (searchRequest == null)
        {
            _logger.LogWarning("Search request {Id} not found", searchRequestId);
            return;
        }

        try
        {
            searchRequest.Status = SearchStatus.Processing;
            await _searchRepository.UpdateAsync(searchRequest);

            var allProducts = searchRequest.SelectedProducts
                .Where(p => p.IsSelected)
                .Concat(searchRequest.AdditionalItems.Where(p => p.IsSelected))
                .ToList();

            var retailers = await _retailerRepository.GetAllAsync();
            var selectedRetailers = retailers
                .Where(r => searchRequest.SelectedRetailerIds.Contains(r.Id) && r.IsEnabled)
                .ToList();

            var report = new SearchReport
            {
                SearchRequestId = searchRequestId,
                UserId = searchRequest.UserId,
                QuerySummary = searchRequest.NaturalLanguageQuery
            };

            foreach (var product in allProducts)
            {
                var comparison = new ProductComparison
                {
                    ProductSelectionId = product.Id,
                    ProductName = product.Name,
                    SearchTerm = product.SearchTerm,
                    IsAdditional = product.IsAdditional
                };

                foreach (var retailer in selectedRetailers)
                {
                    var client = _retailerClients.FirstOrDefault(c => c.RetailerId == retailer.Id);
                    if (client == null) continue;

                    try
                    {
                        var results = await client.SearchProductAsync(product.SearchTerm, retailer);
                        comparison.RetailerResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error searching {Retailer} for {Product}", retailer.Name, product.SearchTerm);
                    }
                }

                report.ProductComparisons.Add(comparison);
            }

            foreach (var retailer in selectedRetailers)
            {
                var total = new RetailerTotal
                {
                    RetailerId = retailer.Id,
                    RetailerName = retailer.Name,
                    ProductsFound = 0,
                    ProductsNotFound = 0
                };

                foreach (var comparison in report.ProductComparisons.Where(p => !p.IsAdditional))
                {
                    var bestResult = comparison.RetailerResults
                        .Where(r => r.RetailerId == retailer.Id && r.IsAvailable)
                        .OrderBy(r => r.NormalizedPrice ?? r.Price)
                        .FirstOrDefault();

                    if (bestResult != null)
                    {
                        total.TotalPrice += bestResult.Price;
                        total.ProductsFound++;
                    }
                    else
                    {
                        total.ProductsNotFound++;
                    }
                }

                total.TotalPriceDisplay = $"${total.TotalPrice:F2}";
                report.RetailerTotals[retailer.Id] = total;
            }

            var bestRetailer = report.RetailerTotals.Values
                .Where(t => t.ProductsNotFound == 0)
                .OrderBy(t => t.TotalPrice)
                .FirstOrDefault()
                ?? report.RetailerTotals.Values.OrderByDescending(t => t.ProductsFound)
                    .ThenBy(t => t.TotalPrice)
                    .FirstOrDefault();

            if (bestRetailer != null)
            {
                report.RecommendedRetailerId = bestRetailer.RetailerId;
                report.RecommendedRetailerName = bestRetailer.RetailerName;
                report.RecommendationReason = bestRetailer.ProductsNotFound == 0
                    ? $"{bestRetailer.RetailerName} has all products at the best total price of {bestRetailer.TotalPriceDisplay}."
                    : $"{bestRetailer.RetailerName} has the most products available at {bestRetailer.TotalPriceDisplay}.";
            }

            var savedReport = await _reportRepository.CreateAsync(report);

            searchRequest.Status = SearchStatus.Completed;
            searchRequest.ReportId = savedReport.Id;
            searchRequest.CompletedAt = DateTime.UtcNow;
            await _searchRepository.UpdateAsync(searchRequest);

            _logger.LogInformation("Search {Id} completed with report {ReportId}", searchRequestId, savedReport.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing search {Id}", searchRequestId);
            searchRequest.Status = SearchStatus.Failed;
            searchRequest.ErrorMessage = ex.Message;
            await _searchRepository.UpdateAsync(searchRequest);
        }
    }
}
