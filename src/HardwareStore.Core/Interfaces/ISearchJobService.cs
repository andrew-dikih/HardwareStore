namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface ISearchJobService
{
    Task EnqueueSearchAsync(string searchRequestId);
    Task ProcessSearchAsync(string searchRequestId);
}
