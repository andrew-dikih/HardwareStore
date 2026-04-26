namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface IRetailerSearchClient
{
    string RetailerId { get; }
    string RetailerName { get; }
    Task<List<RetailerProductResult>> SearchProductAsync(string searchTerm, Retailer retailer);
}
