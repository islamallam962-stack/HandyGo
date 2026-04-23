using Microsoft.AspNetCore.SignalR;

public class NotificationHub : Hub
{
   
    public async Task SendNotification(int userId)
    {
        await Clients.User(userId.ToString()).SendAsync("ReceiveNotification");
    }
}
