using HandyGo.web.Data;
using HandyGo.web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using HandyGo.web.Hubs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class NotificationController : Controller
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly AppDbContext _context;

        
        public NotificationController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext; 
        }

        [HttpGet]
        public IActionResult GetMyNotifications()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { count = 0, items = new List<object>() });

            var notifications = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(15) 
                .Select(n => new {
                    id = n.Id,
                    message = n.Message,
                    link = n.Link,
                    time = n.CreatedAt.ToString("g"),
                    isRead = n.IsRead
                }).ToList();

            var unreadCount = _context.Notifications.Count(n => n.UserId == userId && !n.IsRead);

            return Json(new { count = unreadCount, items = notifications });
        }

        public IActionResult Open(int id)
        {
            var notif = _context.Notifications.Find(id);
            if (notif != null)
            {
                notif.IsRead = true;
                _context.SaveChanges();
                return Redirect(notif.Link ?? "/Profile/Index");
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult ClearAll()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false });

            var userNotifs = _context.Notifications.Where(n => n.UserId == userId);
            _context.Notifications.RemoveRange(userNotifs);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult MarkAsRead()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false });

            var unreadNotifs = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
            foreach (var notif in unreadNotifs)
            {
                notif.IsRead = true;
            }
            _context.SaveChanges();

            return Json(new { success = true });
        }

        
        public async Task SendInternalNotification(int targetUserId, string message, string link)
        {
            var notif = new Notification
            {
                UserId = targetUserId,
                Message = message,
                Link = link,
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");
        }

    }
}
