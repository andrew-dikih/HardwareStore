namespace HardwareStore.Infrastructure.Repositories;
using System.Diagnostics.CodeAnalysis;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

[ExcludeFromCodeCoverage]
public class ReportRepository : IReportRepository
{
    private readonly CosmosDbContext _context;

    public ReportRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<SearchReport?> GetByIdAsync(string id)
    {
        try
        {
            var container = _context.GetContainer();
            var response = await container.ReadItemAsync<SearchReport>(id, new PartitionKey("searchreport"));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SearchReport>> GetByUserIdAsync(string userId)
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<SearchReport>()
            .Where(r => r.DocumentType == "searchreport" && r.UserId == userId)
            .ToFeedIterator();
        
        var reports = new List<SearchReport>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            reports.AddRange(results);
        }
        return reports.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<SearchReport> CreateAsync(SearchReport report)
    {
        var container = _context.GetContainer();
        var response = await container.CreateItemAsync(report, new PartitionKey(report.DocumentType));
        return response.Resource;
    }
}
