using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace HandyGo.web.Hubs
{
    public class ChatHub : Hub
    {
        
        public async Task JoinChat(int requestId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, requestId.ToString());
        }
    }
}
