namespace HardwareStore.Infrastructure.Repositories;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

public class SearchRepository : ISearchRepository
{
    private readonly CosmosDbContext _context;

    public SearchRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<SearchRequest?> GetByIdAsync(string id)
    {
        try
        {
            var container = _context.GetContainer();
            var response = await container.ReadItemAsync<SearchRequest>(id, new PartitionKey("searchrequest"));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SearchRequest>> GetByUserIdAsync(string userId)
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<SearchRequest>()
            .Where(s => s.DocumentType == "searchrequest" && s.UserId == userId)
            .ToFeedIterator();
        
        var requests = new List<SearchRequest>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            requests.AddRange(results);
        }
        return requests.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<SearchRequest> CreateAsync(SearchRequest request)
    {
        var container = _context.GetContainer();
        var response = await container.CreateItemAsync(request, new PartitionKey(request.DocumentType));
        return response.Resource;
    }

    public async Task<SearchRequest> UpdateAsync(SearchRequest request)
    {
        var container = _context.GetContainer();
        var response = await container.UpsertItemAsync(request, new PartitionKey(request.DocumentType));
        return response.Resource;
    }
}
