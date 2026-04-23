using HandyGo.web.Data;
using HandyGo.web.Hubs;
using HandyGo.web.Models;
using HandyGo.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class ChatController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IHubContext<NotificationHub> _notificationHubContext;

        public ChatController(AppDbContext context,
                              IHubContext<ChatHub> chatHubContext,
                              IHubContext<NotificationHub> notificationHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _notificationHubContext = notificationHubContext;
        }

        public IActionResult Index(int requestId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .Include(r => r.Client)
                .Include(r => r.Technician)
                .FirstOrDefault(r => r.Id == requestId && (r.ClientId == userId || r.TechnicianId == userId));

            if (request == null)
            {
                TempData["Error"] = "Unauthorized access or chat not found.";
                return RedirectToAction("Inbox");
            }

            var otherUser = request.Client.Id == userId
                ? request.Technician
                : request.Client;

            ViewBag.OtherUserId = otherUser.Id;
            ViewBag.OtherUserName = otherUser.Name;
            ViewBag.OtherUserImage = otherUser.ImagePath ?? "/images/default.png";
            ViewBag.MyUserImage = _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.ImagePath)
                .FirstOrDefault() ?? "/images/default.png";

            var messages = _context.Messages
                .Where(m => m.RequestId == requestId)
                .OrderBy(m => m.SentAt)
                .ToList();

            ViewBag.RequestId = requestId;
            ViewBag.UserId = userId;

            var unseen = _context.Messages
                .Where(m => m.RequestId == requestId && m.SenderId != userId && !m.IsSeen)
                .ToList();

            if (unseen.Any())
            {
                foreach (var msg in unseen)
                    msg.IsSeen = true;

                _context.SaveChanges();
            }

            return View(messages);
        }

        public IActionResult Inbox()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var inbox = _context.Requests
                .Where(r => (r.ClientId == userId || r.TechnicianId == userId)
                            && _context.Messages.Any(m => m.RequestId == r.Id)
                            && r.Status != "Closed")
                .Select(r => new ChatInboxVM
                {
                    RequestId = r.Id,
                    Status = r.Status,
                    UnseenCount = _context.Messages.Count(m => m.RequestId == r.Id && m.SenderId != userId && !m.IsSeen),
                    LastMessage = _context.Messages.Where(m => m.RequestId == r.Id)
                        .OrderByDescending(m => m.SentAt).Select(m => m.Text)
                        .FirstOrDefault(),
                    LastMessageTime = _context.Messages.Where(m => m.RequestId == r.Id)
                        .OrderByDescending(m => m.SentAt).Select(m => m.SentAt)
                        .FirstOrDefault(),
                    OtherUserName = r.ClientId == userId ? r.Technician.Name : r.Client.Name,
                    OtherUserImage = r.ClientId == userId ? r.Technician.ImagePath : r.Client.ImagePath,
                    OtherUserId = r.ClientId == userId ? r.Technician.Id : r.Client.Id
                })
                .OrderByDescending(i => i.LastMessageTime)
                .ToList();

            return View(inbox);
        }

        [HttpPost]
        public async Task<IActionResult> Send(int requestId, string text, IFormFile image)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            string imagePath = null;
            if (image != null && image.Length > 0)
            {
                var extension = Path.GetExtension(image.FileName);
                var fileName = Guid.NewGuid().ToString() + extension;
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/chat", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath)); 

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                imagePath = "/images/chat/" + fileName;
            }

            if (string.IsNullOrWhiteSpace(text) && imagePath == null)
                return RedirectToAction("Index", new { requestId });

            var message = new Message
            {
                RequestId = requestId,
                SenderId = userId.Value,
                Text = text ?? "", 
                ImagePath = imagePath,
                SentAt = DateTime.Now,
                IsSeen = false
            };

            _context.Messages.Add(message);

            var request = _context.Requests.Find(requestId);
            if (request != null)
            {
                var receiverId = (request.ClientId == userId) ? request.TechnicianId : request.ClientId;

                var notification = new Notification
                {
                    UserId = receiverId.Value,
                    Message = $"?? New message from {HttpContext.Session.GetString("UserName")}",
                    Link = $"/Chat/Index?requestId={requestId}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            var userImage = HttpContext.Session.GetString("UserImage") ?? "/images/default.png";
            await _chatHubContext.Clients.Group(requestId.ToString()).SendAsync("ReceiveMessage", new
            {
                senderId = userId.Value,
                text = text ?? "",
                imagePath = imagePath ?? "",
                sentAt = message.SentAt.ToString("HH:mm"),
                avatar = userImage
            });

            await _notificationHubContext.Clients.All.SendAsync("ReceiveNotification");

            return RedirectToAction("Index", new { requestId });
        }

        [HttpPost]
        public IActionResult MarkAsRead(int requestId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false });

            var unseenMessages = _context.Messages
                .Where(m => m.RequestId == requestId && m.SenderId != userId && !m.IsSeen)
                .ToList();

            if (unseenMessages.Any())
            {
                foreach (var msg in unseenMessages) msg.IsSeen = true;
                _context.SaveChanges();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult DeleteChat(int requestId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .FirstOrDefault(r => r.Id == requestId && (r.ClientId == userId || r.TechnicianId == userId));

            if (request == null)
            {
                TempData["Error"] = "Unauthorized action.";
                return RedirectToAction("Inbox");
            }

            var messages = _context.Messages.Where(m => m.RequestId == requestId).ToList();
            _context.Messages.RemoveRange(messages);
            request.Status = "Closed";

            _context.SaveChanges();

            TempData["Success"] = "Chat deleted successfully.";
            return RedirectToAction("Inbox");
        }

        [HttpPost]
        public IActionResult StartChat(int otherUserId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .Where(r => r.Status != "Closed" &&
                    ((r.ClientId == userId && r.TechnicianId == otherUserId) ||
                     (r.ClientId == otherUserId && r.TechnicianId == userId)))
                .Select(r => new
                {
                    Request = r,

                    LastMessageTime = _context.Messages
                                        .Where(m => m.RequestId == r.Id)
                                        .Max(m => (DateTime?)m.SentAt) ?? r.CreatedAt
                })
                .OrderByDescending(x => x.LastMessageTime)
                .Select(x => x.Request)
                .FirstOrDefault();

            if (request == null)
            {
                request = new Request
                {
                    ClientId = userId.Value,
                    TechnicianId = otherUserId,
                    ServiceType = "Chat",
                    Address = "Chat Only",
                    Status = "Accepted",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now 
                };
                _context.Requests.Add(request);
                _context.SaveChanges();
            }

            return RedirectToAction("Index", new { requestId = request.Id });
        }

        [HttpGet]
        public IActionResult GetTotalUnseenMessages()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(0);

            var count = _context.Messages
                .Count(m => m.SenderId != userId &&
                            m.IsSeen == false &&
                            m.Request.Status != "Closed" &&
                            (m.Request.ClientId == userId || m.Request.TechnicianId == userId));

            return Json(count);
        }
    }
}
