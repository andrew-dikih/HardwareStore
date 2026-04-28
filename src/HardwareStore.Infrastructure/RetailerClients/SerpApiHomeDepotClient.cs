namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

public class SerpApiHomeDepotClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly SerpApiSettings _settings;
    private readonly ILogger<SerpApiHomeDepotClient> _logger;

    public string RetailerId => "homedepot";
    public string RetailerName => "Home Depot";

    public SerpApiHomeDepotClient(
        IHttpClientFactory httpClientFactory,
        IOptions<SerpApiSettings> settings,
        ILogger<SerpApiHomeDepotClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("serpapi");
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        try
        {
            var url = $"{_settings.BaseUrl}/search.json?engine=home_depot&q={Uri.EscapeDataString(searchTerm)}&api_key={_settings.ApiKey}";
            var response = await _httpClient.GetFromJsonAsync<HomeDepotSearchResponse>(url);

            if (response?.Products == null)
                return [];

            return response.Products
                .Select(MapProduct)
                .Where(p => p.Price > 0)
                .Take(10)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Home Depot results via SerpAPI for '{SearchTerm}'", searchTerm);
            return [];
        }
    }

    private static RetailerProductResult MapProduct(HomeDepotProduct p) => new()
    {
        RetailerId = "homedepot",
        RetailerName = "Home Depot",
        ProductTitle = p.Title ?? string.Empty,
        ProductUrl = NormalizeHomeDepotUrl(p.Link) ?? string.Empty,
        ImageUrl = NormalizeHomeDepotUrl(p.Thumbnail),
        Price = p.Price,
        PriceDisplay = p.Price > 0 ? $"${p.Price:F2}" : string.Empty,
        IsAvailable = true,
        Sku = p.ProductId
    };

    private static string? NormalizeHomeDepotUrl(string? url) =>
        url?.Replace("apionline.homedepot.com", "www.homedepot.com", StringComparison.OrdinalIgnoreCase);

    private sealed class HomeDepotSearchResponse
    {
        [JsonPropertyName("products")]
        public List<HomeDepotProduct>? Products { get; set; }
    }

    private sealed class HomeDepotProduct
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }
    }
}
