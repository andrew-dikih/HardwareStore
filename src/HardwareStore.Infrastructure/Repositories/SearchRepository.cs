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

    public async Task<List<SearchRequest>> GetPendingAsync()
    {
        var container = _context.GetContainer();
        var now = DateTime.UtcNow;

        var query = container.GetItemLinqQueryable<SearchRequest>()
            .Where(s => s.DocumentType == "searchrequest" &&
                        (s.Status == SearchStatus.Queued ||
                         (s.Status == SearchStatus.Processing && (s.LeaseExpiresAt == null || s.LeaseExpiresAt < now))))
            .ToFeedIterator();

        var results = new List<SearchRequest>();
        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }

    public async Task<bool> TryAcquireLeaseAsync(string id, string instanceId, TimeSpan leaseDuration)
    {
        var container = _context.GetContainer();

        try
        {
            var readResponse = await container.ReadItemAsync<SearchRequest>(id, new PartitionKey("searchrequest"));
            var request = readResponse.Resource;
            var etag = readResponse.ETag;
            var now = DateTime.UtcNow;

            var canAcquire = request.Status == SearchStatus.Queued ||
                             (request.Status == SearchStatus.Processing &&
                              (request.LeaseExpiresAt == null || request.LeaseExpiresAt < now));

            if (!canAcquire)
                return false;

            request.ProcessingInstanceId = instanceId;
            request.LeaseExpiresAt = now.Add(leaseDuration);

            await container.ReplaceItemAsync(
                request,
                id,
                new PartitionKey("searchrequest"),
                new ItemRequestOptions { IfMatchEtag = etag });

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // Another instance updated the document between our read and write
            return false;
        }
    }
}
