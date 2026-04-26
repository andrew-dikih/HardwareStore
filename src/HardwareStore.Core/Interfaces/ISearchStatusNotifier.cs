namespace HardwareStore.Core.Interfaces;

public interface ISearchStatusNotifier
{
    Task NotifyStatusChangedAsync(string searchRequestId, string status, string? reportId = null, string? errorMessage = null);
}
