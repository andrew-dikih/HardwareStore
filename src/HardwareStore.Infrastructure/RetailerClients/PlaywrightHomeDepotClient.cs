namespace HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Microsoft.Extensions.Logging;

public class PlaywrightHomeDepotClient : IRetailerSearchClient
{
    private readonly PlaywrightBrowserService _browser;
    private readonly ILogger<PlaywrightHomeDepotClient> _logger;

    public string RetailerId => "homedepot";
    public string RetailerName => "Home Depot";

    public PlaywrightHomeDepotClient(PlaywrightBrowserService browser, ILogger<PlaywrightHomeDepotClient> logger)
    {
        _browser = browser;
        _logger = logger;
    }

    public async Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer)
    {
        try
        {
            var url = $"https://www.homedepot.com/s/{Uri.EscapeDataString(searchTerm)}";
            var html = await _browser.GetHtmlAsync(url);
            return HomeDepotClient.ParseSearchResults(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playwright error fetching Home Depot results for '{SearchTerm}'", searchTerm);
            return [];
        }
    }
}
