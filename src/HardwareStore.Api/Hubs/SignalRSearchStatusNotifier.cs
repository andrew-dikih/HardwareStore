namespace HardwareStore.Api.Hubs;
using HardwareStore.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

public class SignalRSearchStatusNotifier : ISearchStatusNotifier
{
    private readonly IHubContext<SearchStatusHub> _hubContext;

    public SignalRSearchStatusNotifier(IHubContext<SearchStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyStatusChangedAsync(string searchRequestId, string status, string? reportId = null, string? errorMessage = null)
    {
        await _hubContext.Clients.Group(searchRequestId).SendAsync("SearchStatusChanged", new
        {
            id = searchRequestId,
            status,
            reportId,
            errorMessage
        });
    }
}
