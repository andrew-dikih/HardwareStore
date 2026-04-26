namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using System.Web;

public class LowesClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;

    public string RetailerId => "lowes";
    public string RetailerName => "Lowe's";

    public LowesClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("lowes");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        var results = new List<RetailerProductResult>();
        try
        {
            results.AddRange(await GetMockResultsAsync(searchTerm, RetailerId, RetailerName, "https://www.lowes.com"));
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
                ProductTitle = $"{searchTerm} - Single Unit",
                ProductUrl = $"{baseUrl}/search?searchTerm={HttpUtility.UrlEncode(searchTerm)}",
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
                ProductTitle = $"{searchTerm} - Bulk Pack (12 count)",
                ProductUrl = $"{baseUrl}/search?searchTerm={HttpUtility.UrlEncode(searchTerm)}&pack=12",
                Price = GetMockPrice(searchTerm, 10.2m),
                PriceDisplay = $"${GetMockPrice(searchTerm, 10.2m):F2}",
                PackageSize = "12 Pack",
                QuantityInPackage = 12,
                Unit = "each",
                NormalizedPrice = GetMockPrice(searchTerm, 10.2m) / 12,
                NormalizedPriceDisplay = $"${GetMockPrice(searchTerm, 10.2m) / 12:F2} each",
                IsAvailable = true
            }
        };
        return Task.FromResult(results);
    }

    private static decimal GetMockPrice(string term, decimal baseMultiplier)
    {
        var hash = term.Length * 3 + term.Sum(c => (int)c) % 100;
        return Math.Round((3.79m + hash % 20) * baseMultiplier, 2);
    }
}
