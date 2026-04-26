namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface IReportRepository
{
    Task<SearchReport?> GetByIdAsync(string id);
    Task<List<SearchReport>> GetByUserIdAsync(string userId);
    Task<SearchReport> CreateAsync(SearchReport report);
}
