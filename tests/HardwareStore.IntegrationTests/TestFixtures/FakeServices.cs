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
