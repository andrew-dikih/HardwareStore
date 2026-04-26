namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class HomeDepotClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HomeDepotClient> _logger;

    public string RetailerId => "homedepot";
    public string RetailerName => "Home Depot";

    public HomeDepotClient(IHttpClientFactory httpClientFactory, ILogger<HomeDepotClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("homedepot");
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        var results = new List<RetailerProductResult>();
        try
        {
            var url = $"https://www.homedepot.com/s/{Uri.EscapeDataString(searchTerm)}";
            var html = await _httpClient.GetStringAsync(url);
            results.AddRange(ParseSearchResults(html));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching or parsing Home Depot results for '{SearchTerm}'", searchTerm);
        }
        return results;
    }

    // Internal for unit testing
    internal static List<RetailerProductResult> ParseSearchResults(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nextDataNode = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
            if (nextDataNode == null) return [];

            using var jsonDoc = JsonDocument.Parse(nextDataNode.InnerText);
            var root = jsonDoc.RootElement;

            if (!TryNavigatePath(root, ["props", "pageProps", "initialData", "searchReport"], out var searchReport))
                return [];

            if (!searchReport.TryGetProperty("products", out var products) ||
                products.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<RetailerProductResult>();
            foreach (var product in products.EnumerateArray().Take(10))
            {
                var result = ParseProduct(product);
                if (result != null) results.Add(result);
            }
            return results;
        }
        catch
        {
            // The page structure is controlled by homedepot.com and can change at any time.
            // Return empty rather than propagate so one bad response never breaks a search job.
            return [];
        }
    }

    private static RetailerProductResult? ParseProduct(JsonElement product)
    {
        var title = product.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title)) return null;

        decimal price = 0;
        if (product.TryGetProperty("pricing", out var pricing) &&
            pricing.TryGetProperty("value", out var priceVal))
        {
            priceVal.TryGetDecimal(out price);
        }

        if (price <= 0) return null;

        var canonicalUrl = product.TryGetProperty("canonicalUrl", out var urlEl) ? urlEl.GetString() ?? "" : "";
        var sku = product.TryGetProperty("itemId", out var skuEl) ? skuEl.GetString() : null;

        string? imageUrl = null;
        if (product.TryGetProperty("media", out var media) &&
            media.TryGetProperty("images", out var images) &&
            images.ValueKind == JsonValueKind.Array &&
            images.GetArrayLength() > 0 &&
            images[0].TryGetProperty("url", out var imgUrl))
        {
            imageUrl = imgUrl.GetString();
        }

        return new RetailerProductResult
        {
            RetailerId = "homedepot",
            RetailerName = "Home Depot",
            ProductTitle = title,
            ProductUrl = canonicalUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? canonicalUrl
                : $"https://www.homedepot.com{canonicalUrl}",
            ImageUrl = imageUrl,
            Price = price,
            PriceDisplay = $"${price:F2}",
            IsAvailable = true,
            Sku = sku
        };
    }

    private static bool TryNavigatePath(JsonElement element, string[] path, out JsonElement result)
    {
        result = element;
        foreach (var key in path)
        {
            if (!result.TryGetProperty(key, out result))
                return false;
        }
        return true;
    }
}
