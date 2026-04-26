using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using System.Collections.Concurrent;

namespace HardwareStore.IntegrationTests.TestFixtures;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _store = new();

    public Task<User?> GetByIdAsync(string id) =>
        Task.FromResult(_store.TryGetValue(id, out var u) ? u : null);

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult(_store.Values.FirstOrDefault(u => u.Email == email));

    public Task<List<User>> GetAllAsync() =>
        Task.FromResult(_store.Values.ToList());

    public Task<List<User>> GetPendingApprovalsAsync() =>
        Task.FromResult(_store.Values.Where(u => u.Status == UserStatus.PendingApproval).ToList());

    public Task<User> CreateAsync(User user)
    {
        _store[user.Id] = user;
        return Task.FromResult(user);
    }

    public Task<User> UpdateAsync(User user)
    {
        _store[user.Id] = user;
        return Task.FromResult(user);
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class InMemoryRetailerRepository : IRetailerRepository
{
    private readonly ConcurrentDictionary<string, Retailer> _store = new();

    public Task<Retailer?> GetByIdAsync(string id) =>
        Task.FromResult(_store.TryGetValue(id, out var r) ? r : null);

    public Task<List<Retailer>> GetAllAsync() =>
        Task.FromResult(_store.Values.ToList());

    public Task<List<Retailer>> GetEnabledAsync() =>
        Task.FromResult(_store.Values.Where(r => r.IsEnabled).ToList());

    public Task<Retailer> CreateAsync(Retailer retailer)
    {
        _store[retailer.Id] = retailer;
        return Task.FromResult(retailer);
    }

    public Task<Retailer> UpdateAsync(Retailer retailer)
    {
        _store[retailer.Id] = retailer;
        return Task.FromResult(retailer);
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class InMemorySearchRepository : ISearchRepository
{
    private readonly ConcurrentDictionary<string, SearchRequest> _store = new();

    public Task<SearchRequest?> GetByIdAsync(string id) =>
        Task.FromResult(_store.TryGetValue(id, out var s) ? s : null);

    public Task<List<SearchRequest>> GetByUserIdAsync(string userId) =>
        Task.FromResult(_store.Values.Where(s => s.UserId == userId).ToList());

    public Task<SearchRequest> CreateAsync(SearchRequest request)
    {
        _store[request.Id] = request;
        return Task.FromResult(request);
    }

    public Task<SearchRequest> UpdateAsync(SearchRequest request)
    {
        _store[request.Id] = request;
        return Task.FromResult(request);
    }
}

public class InMemoryReportRepository : IReportRepository
{
    private readonly ConcurrentDictionary<string, SearchReport> _store = new();

    public Task<SearchReport?> GetByIdAsync(string id) =>
        Task.FromResult(_store.TryGetValue(id, out var r) ? r : null);

    public Task<List<SearchReport>> GetByUserIdAsync(string userId) =>
        Task.FromResult(_store.Values.Where(r => r.UserId == userId).ToList());

    public Task<SearchReport> CreateAsync(SearchReport report)
    {
        _store[report.Id] = report;
        return Task.FromResult(report);
    }
}
