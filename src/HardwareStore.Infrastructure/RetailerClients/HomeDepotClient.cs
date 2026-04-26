namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using System.Web;

public class HomeDepotClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;

    public string RetailerId => "homedepot";
    public string RetailerName => "Home Depot";

    public HomeDepotClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("homedepot");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        var results = new List<RetailerProductResult>();
        try
        {
            results.AddRange(await GetMockResultsAsync(searchTerm, RetailerId, RetailerName, "https://www.homedepot.com"));
        }
        catch
        {
            // Return empty on failure
        }
        return results;
    }

    private static Task<List<RetailerProductResult>> GetMockResultsAsync(
        string searchTerm, string retailerId, string retailerName, string baseUrl)
    {
        var results = new List<RetailerProductResult>
        {
            new RetailerProductResult
            {
                RetailerId = retailerId,
                RetailerName = retailerName,
                ProductTitle = $"{searchTerm} - Standard Pack",
                ProductUrl = $"{baseUrl}/search?q={HttpUtility.UrlEncode(searchTerm)}",
                Price = GetMockPrice(searchTerm, 1.0m),
                PriceDisplay = $"${GetMockPrice(searchTerm, 1.0m):F2}",
                PackageSize = "1 Pack",
                QuantityInPackage = 1,
                Unit = "each",
                NormalizedPrice = GetMockPrice(searchTerm, 1.0m),
                NormalizedPriceDisplay = $"${GetMockPrice(searchTerm, 1.0m):F2} each",
                IsAvailable = true
            },
            new RetailerProductResult
            {
                RetailerId = retailerId,
                RetailerName = retailerName,
                ProductTitle = $"{searchTerm} - Value Pack (10 count)",
                ProductUrl = $"{baseUrl}/search?q={HttpUtility.UrlEncode(searchTerm)}&pack=10",
                Price = GetMockPrice(searchTerm, 8.5m),
                PriceDisplay = $"${GetMockPrice(searchTerm, 8.5m):F2}",
                PackageSize = "10 Pack",
                QuantityInPackage = 10,
                Unit = "each",
                NormalizedPrice = GetMockPrice(searchTerm, 8.5m) / 10,
                NormalizedPriceDisplay = $"${GetMockPrice(searchTerm, 8.5m) / 10:F2} each",
                IsAvailable = true
            }
        };
        return Task.FromResult(results);
    }

    private static decimal GetMockPrice(string term, decimal baseMultiplier)
    {
        var hash = term.Length * 3 + term.Sum(c => (int)c) % 100;
        return Math.Round((3.99m + hash % 20) * baseMultiplier, 2);
    }
}
