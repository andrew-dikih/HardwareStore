namespace HardwareStore.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

public class SearchStatusHub : Hub
{
    public async Task JoinSearch(string searchRequestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, searchRequestId);
    }
}
