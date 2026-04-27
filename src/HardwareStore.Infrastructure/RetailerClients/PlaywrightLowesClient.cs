namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.Extensions.Logging;

public class PlaywrightLowesClient : IRetailerSearchClient
{
    private readonly PlaywrightBrowserService _browser;
    private readonly ILogger<PlaywrightLowesClient> _logger;

    public string RetailerId => "lowes";
    public string RetailerName => "Lowe's";

    public PlaywrightLowesClient(PlaywrightBrowserService browser, ILogger<PlaywrightLowesClient> logger)
    {
        _browser = browser;
        _logger = logger;
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        try
        {
            var url = $"https://www.lowes.com/search?searchTerm={Uri.EscapeDataString(searchTerm)}";
            var html = await _browser.GetHtmlAsync(url);
            return LowesClient.ParseSearchResults(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playwright error fetching Lowe's results for '{SearchTerm}'", searchTerm);
            return [];
        }
    }
}
