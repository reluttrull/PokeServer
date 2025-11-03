using Microsoft.AspNetCore.SignalR;

namespace PokeServer
{
    public class NotificationHub : Hub
    {
        public async Task JoinGameGroup(string guid)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, guid);
        }
    }
}
