using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

namespace HardwareStore.IntegrationTests.TestFixtures;

public class NoOpEmailService : IEmailService
{
    public Task SendSignupNotificationAsync(string userEmail, string userName) => Task.CompletedTask;
    public Task SendApprovalNotificationAsync(string userEmail, string userName) => Task.CompletedTask;
    public Task SendRejectionNotificationAsync(string userEmail, string userName) => Task.CompletedTask;
}

public class FakeNaturalLanguageService : INaturalLanguageService
{
    public Task<NaturalLanguageSearchResult> ParseSearchQueryAsync(string query)
    {
        var result = new NaturalLanguageSearchResult
        {
            Summary = query,
            SuggestedProducts =
            [
                new ProductSelection
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = query,
                    SearchTerm = query,
                    IsSelected = true
                }
            ]
        };
        return Task.FromResult(result);
    }
}

public class NoOpSearchStatusNotifier : ISearchStatusNotifier
{
    public Task NotifyStatusChangedAsync(string searchRequestId, string status, string? reportId = null, string? errorMessage = null)
        => Task.CompletedTask;
}

public class FakeRetailerSearchClient : IRetailerSearchClient
{
    public FakeRetailerSearchClient(string retailerId, string retailerName)
    {
        RetailerId = retailerId;
        RetailerName = retailerName;
    }

    public string RetailerId { get; }
    public string RetailerName { get; }

    public Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer) =>
        Task.FromResult(new List<RetailerProductResult>
        {
            new() {
                RetailerId = RetailerId,
                RetailerName = RetailerName,
                ProductTitle = $"Fake {searchTerm} product A",
                Price = 9.99m,
                PriceDisplay = "$9.99",
                ProductUrl = "https://example.com/a",
                IsAvailable = true
            },
            new() {
                RetailerId = RetailerId,
                RetailerName = RetailerName,
                ProductTitle = $"Fake {searchTerm} product B",
                Price = 14.99m,
                PriceDisplay = "$14.99",
                ProductUrl = "https://example.com/b",
                IsAvailable = true
            }
        });
}
