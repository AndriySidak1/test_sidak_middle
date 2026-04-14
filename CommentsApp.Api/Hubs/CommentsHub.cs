using Microsoft.AspNetCore.SignalR;

namespace CommentsApp.Api.Hubs;

public sealed class CommentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", new { message = "Connected to CommentsHub" });
        await base.OnConnectedAsync();
    }
}
