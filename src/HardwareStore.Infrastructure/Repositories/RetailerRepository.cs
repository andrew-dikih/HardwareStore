namespace HardwareStore.Infrastructure.Repositories;
using System.Diagnostics.CodeAnalysis;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

[ExcludeFromCodeCoverage]
public class RetailerRepository : IRetailerRepository
{
    private readonly CosmosDbContext _context;

    public RetailerRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<Retailer?> GetByIdAsync(string id)
    {
        try
        {
            var container = _context.GetContainer();
            var response = await container.ReadItemAsync<Retailer>(id, new PartitionKey("retailer"));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Retailer>> GetAllAsync()
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<Retailer>()
            .Where(r => r.DocumentType == "retailer")
            .ToFeedIterator();
        
        var retailers = new List<Retailer>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            retailers.AddRange(results);
        }
        return retailers;
    }

    public async Task<List<Retailer>> GetEnabledAsync()
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<Retailer>()
            .Where(r => r.DocumentType == "retailer" && r.IsEnabled)
            .ToFeedIterator();
        
        var retailers = new List<Retailer>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            retailers.AddRange(results);
        }
        return retailers;
    }

    public async Task<Retailer> CreateAsync(Retailer retailer)
    {
        var container = _context.GetContainer();
        var response = await container.CreateItemAsync(retailer, new PartitionKey(retailer.DocumentType));
        return response.Resource;
    }

    public async Task<Retailer> UpdateAsync(Retailer retailer)
    {
        var container = _context.GetContainer();
        var response = await container.UpsertItemAsync(retailer, new PartitionKey(retailer.DocumentType));
        return response.Resource;
    }

    public async Task DeleteAsync(string id)
    {
        var container = _context.GetContainer();
        await container.DeleteItemAsync<Retailer>(id, new PartitionKey("retailer"));
    }
}
