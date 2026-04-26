namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<List<User>> GetPendingApprovalsAsync();
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(string id);
}
