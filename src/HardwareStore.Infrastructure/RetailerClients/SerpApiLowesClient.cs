namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

public class SerpApiLowesClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly SerpApiSettings _settings;
    private readonly ILogger<SerpApiLowesClient> _logger;

    public string RetailerId => "lowes";
    public string RetailerName => "Lowe's";

    public SerpApiLowesClient(
        IHttpClientFactory httpClientFactory,
        IOptions<SerpApiSettings> settings,
        ILogger<SerpApiLowesClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("serpapi");
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        try
        {
            var query = Uri.EscapeDataString($"{searchTerm} lowes");
            var url = $"{_settings.BaseUrl}/search.json?engine=google_shopping&q={query}&api_key={_settings.ApiKey}&num=10";
            var response = await _httpClient.GetFromJsonAsync<GoogleShoppingSearchResponse>(url);

            if (response?.ShoppingResults == null)
                return [];

            return response.ShoppingResults
                .Where(IsLowesResult)
                .Select(MapProduct)
                .Where(p => p.Price > 0)
                .Take(10)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Lowe's results via SerpAPI for '{SearchTerm}'", searchTerm);
            return [];
        }
    }

    private static bool IsLowesResult(ShoppingResult r) =>
        (r.Source != null && r.Source.Contains("Lowe", StringComparison.OrdinalIgnoreCase)) ||
        (r.DirectLink != null && r.DirectLink.Contains("lowes.com", StringComparison.OrdinalIgnoreCase)) ||
        (r.Link != null && r.Link.Contains("lowes.com", StringComparison.OrdinalIgnoreCase));

    private static RetailerProductResult MapProduct(ShoppingResult r) => new()
    {
        RetailerId = "lowes",
        RetailerName = "Lowe's",
        ProductTitle = r.Title ?? string.Empty,
        ProductUrl = r.DirectLink ?? r.ProductLink ?? r.Link ?? string.Empty,
        ImageUrl = r.Thumbnail,
        Price = ParsePrice(r.Price),
        PriceDisplay = r.Price ?? string.Empty,
        IsAvailable = true,
        Sku = null
    };

    internal static decimal ParsePrice(string? priceStr)
    {
        if (string.IsNullOrWhiteSpace(priceStr))
            return 0;

        // Take the first value from ranges like "$24.98 - $39.98"
        var first = priceStr.Split('-')[0];
        var cleaned = first.Replace("$", "").Trim();
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private sealed class GoogleShoppingSearchResponse
    {
        [JsonPropertyName("shopping_results")]
        public List<ShoppingResult>? ShoppingResults { get; set; }
    }

    private sealed class ShoppingResult
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("price")]
        public string? Price { get; set; }

        [JsonPropertyName("direct_link")]
        public string? DirectLink { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("product_link")]
        public string? ProductLink { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
