namespace HardwareStore.Infrastructure.Repositories;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

using CoreUser = HardwareStore.Core.Models.User;

public class UserRepository : IUserRepository
{
    private readonly CosmosDbContext _context;

    public UserRepository(CosmosDbContext context)
    {
        _context = context;
    }

    public async Task<CoreUser?> GetByIdAsync(string id)
    {
        try
        {
            var container = _context.GetContainer();
            var response = await container.ReadItemAsync<CoreUser>(id, new PartitionKey("user"));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<CoreUser?> GetByEmailAsync(string email)
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<CoreUser>()
            .Where(u => u.DocumentType == "user" && u.Email == email)
            .ToFeedIterator();
        
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            var user = results.FirstOrDefault();
            if (user != null) return user;
        }
        return null;
    }

    public async Task<List<CoreUser>> GetAllAsync()
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<CoreUser>()
            .Where(u => u.DocumentType == "user")
            .ToFeedIterator();
        
        var users = new List<CoreUser>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            users.AddRange(results);
        }
        return users;
    }

    public async Task<List<CoreUser>> GetPendingApprovalsAsync()
    {
        var container = _context.GetContainer();
        var query = container.GetItemLinqQueryable<CoreUser>()
            .Where(u => u.DocumentType == "user" && u.Status == UserStatus.PendingApproval)
            .ToFeedIterator();
        
        var users = new List<CoreUser>();
        while (query.HasMoreResults)
        {
            var results = await query.ReadNextAsync();
            users.AddRange(results);
        }
        return users;
    }

    public async Task<CoreUser> CreateAsync(CoreUser user)
    {
        var container = _context.GetContainer();
        var response = await container.CreateItemAsync(user, new PartitionKey(user.DocumentType));
        return response.Resource;
    }

    public async Task<CoreUser> UpdateAsync(CoreUser user)
    {
        var container = _context.GetContainer();
        var response = await container.UpsertItemAsync(user, new PartitionKey(user.DocumentType));
        return response.Resource;
    }

    public async Task DeleteAsync(string id)
    {
        var container = _context.GetContainer();
        await container.DeleteItemAsync<CoreUser>(id, new PartitionKey("user"));
    }
}
