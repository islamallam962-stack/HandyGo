using Microsoft.AspNetCore.Mvc;
using HandyGo.web.Data;
using System.Linq;

public class ChatNotificationViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public ChatNotificationViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Content("");

       
        var unseenCount = _context.Messages
            .Count(m => m.RequestId != null && m.SenderId != userId && !m.IsSeen);

        return View(unseenCount);
    }
}
