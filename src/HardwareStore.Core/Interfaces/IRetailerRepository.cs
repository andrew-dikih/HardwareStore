namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface IRetailerRepository
{
    Task<Retailer?> GetByIdAsync(string id);
    Task<List<Retailer>> GetAllAsync();
    Task<List<Retailer>> GetEnabledAsync();
    Task<Retailer> CreateAsync(Retailer retailer);
    Task<Retailer> UpdateAsync(Retailer retailer);
    Task DeleteAsync(string id);
}
