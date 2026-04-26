namespace HardwareStore.Infrastructure.Services;
using HardwareStore.Core.Interfaces;

public class NoOpSearchStatusNotifier : ISearchStatusNotifier
{
    public Task NotifyStatusChangedAsync(string searchRequestId, string status, string? reportId = null, string? errorMessage = null)
        => Task.CompletedTask;
}
