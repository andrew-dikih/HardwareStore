namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class LowesClient : IRetailerSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LowesClient> _logger;

    public string RetailerId => "lowes";
    public string RetailerName => "Lowe's";

    public LowesClient(IHttpClientFactory httpClientFactory, ILogger<LowesClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("lowes");
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
            var url = $"https://www.lowes.com/search?searchTerm={Uri.EscapeDataString(searchTerm)}";
            var html = await _httpClient.GetStringAsync(url);
            results.AddRange(ParseSearchResults(html));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching or parsing Lowe's results for '{SearchTerm}'", searchTerm);
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

            // Try known paths for products in the Lowe's page data
            JsonElement products;
            if (TryNavigatePath(root, ["props", "pageProps", "searchModel", "products"], out var p1))
                products = p1;
            else if (TryNavigatePath(root, ["props", "pageProps", "searchData", "products"], out var p2))
                products = p2;
            else
                return [];

            if (products.ValueKind != JsonValueKind.Array) return [];

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
            // The page structure is controlled by lowes.com and can change at any time.
            // Return empty rather than propagate so one bad response never breaks a search job.
            return [];
        }
    }

    private static RetailerProductResult? ParseProduct(JsonElement product)
    {
        var title = product.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title)) return null;

        decimal price = 0;
        if (product.TryGetProperty("pricing", out var pricing))
        {
            if (pricing.TryGetProperty("amount", out var amount))
                amount.TryGetDecimal(out price);
            else if (pricing.TryGetProperty("value", out var value))
                value.TryGetDecimal(out price);
        }

        if (price <= 0) return null;

        var productUrl = product.TryGetProperty("productUrl", out var urlEl) ? urlEl.GetString() ?? "" : "";
        var sku = product.TryGetProperty("itemid", out var skuEl)
            ? skuEl.GetString()
            : product.TryGetProperty("productId", out var pidEl) ? pidEl.GetString() : null;

        var imageUrl = product.TryGetProperty("imageUrl", out var imgEl) ? imgEl.GetString() : null;

        return new RetailerProductResult
        {
            RetailerId = "lowes",
            RetailerName = "Lowe's",
            ProductTitle = title,
            ProductUrl = BuildProductUrl(productUrl, sku, title),
            ImageUrl = imageUrl,
            Price = price,
            PriceDisplay = $"${price:F2}",
            IsAvailable = true,
            Sku = sku
        };
    }

    // Internal for unit testing
    internal static string BuildProductUrl(string productUrl, string? sku, string title)
    {
        if (!string.IsNullOrEmpty(productUrl))
        {
            return productUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? productUrl
                : $"https://www.lowes.com{productUrl}";
        }

        if (!string.IsNullOrEmpty(sku) && !string.IsNullOrEmpty(title))
        {
            var slug = BuildLowesSlug(title);
            return $"https://www.lowes.com/pd/{slug}/{sku}";
        }

        return string.Empty;
    }

    // Internal for unit testing
    internal static string BuildLowesSlug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c) || c == '-')
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
            {
                sb.Append('-');
            }
        }
        // Remove trailing hyphen
        if (sb.Length > 0 && sb[sb.Length - 1] == '-')
            sb.Length--;
        return sb.ToString();
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
