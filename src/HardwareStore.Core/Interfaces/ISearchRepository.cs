namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface ISearchRepository
{
    Task<SearchRequest?> GetByIdAsync(string id);
    Task<List<SearchRequest>> GetByUserIdAsync(string userId);
    Task<SearchRequest> CreateAsync(SearchRequest request);
    Task<SearchRequest> UpdateAsync(SearchRequest request);
    Task<List<SearchRequest>> GetPendingAsync();
    Task<bool> TryAcquireLeaseAsync(string id, string instanceId, TimeSpan leaseDuration);
}
