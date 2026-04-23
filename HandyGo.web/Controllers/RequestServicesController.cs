using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HandyGo.web.Data;
using HandyGo.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using HandyGo.web.Hubs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class RequestServicesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public RequestServicesController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null || !currentUser.IsActive)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            ViewBag.CurrentUserAddress = currentUser?.Address ?? "";
            ViewBag.CurrentUserLat = currentUser?.Latitude;
            ViewBag.CurrentUserLng = currentUser?.Longitude;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string ServiceType, string Address, string AssignmentMethod, double? Latitude, double? Longitude)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            var clientName = HttpContext.Session.GetString("UserName") ?? "A client";

            if (clientId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(ServiceType) || string.IsNullOrWhiteSpace(Address) || !Latitude.HasValue || !Longitude.HasValue)
            {
                TempData["Error"] = "Please select a service type and pin your location strictly on the map.";
                return RedirectToAction("Create", "RequestServices");
            }

            var client = await _context.Users.FindAsync(clientId.Value);
            if (client == null || !client.IsActive) return RedirectToAction("Login", "Account");

            client.Address = Address.Trim();
            client.Latitude = Latitude;
            client.Longitude = Longitude;
            client.LocationUpdatedAt = DateTime.Now;
            _context.Update(client);

            if (AssignmentMethod == "Public")
            {
                var publicRequest = new Request
                {
                    ClientId = clientId.Value,
                    TechnicianId = null,
                    ServiceType = ServiceType,
                    Address = Address.Trim(),
                    Latitude = Latitude,
                    Longitude = Longitude,
                    Status = "OpenForBids",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Requests.Add(publicRequest);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNotification");

                TempData["Success"] = "Your request is now public! Technicians will send you offers soon.";
                TempData["ActiveTab"] = "requestsTab"; 
                return RedirectToAction("Index", "Profile");
            }

            var technicians = _context.Users
                .Where(u => u.Role == "Technician"
                         && u.Category.ToLower().Trim() == ServiceType.ToLower().Trim()
                         && u.IsActive == true)
                .ToList();

            if (!technicians.Any())
            {
                TempData["Error"] = $"Sorry, no technicians available for {ServiceType} right now.";
                return RedirectToAction("Create", "RequestServices");
            }

            var selectedTech = technicians
                .OrderBy(t => _context.Requests.Count(r => r.TechnicianId == t.Id && r.Status == "Pending"))
                .First();

            var request = new Request
            {
                ClientId = clientId.Value,
                TechnicianId = selectedTech.Id,
                ServiceType = ServiceType,
                Address = Address.Trim(),
                Latitude = Latitude,
                Longitude = Longitude,
                Status = "Pending",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Requests.Add(request);
            _context.Notifications.Add(new Notification
            {
                UserId = selectedTech.Id,
                Message = $"?? {clientName} requested a {ServiceType} service via Smart Match!",
                Link = "/TechnicianRequests/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = $"Smart Match found a technician! {selectedTech.Name} is reviewing your request.";
            TempData["ActiveTab"] = "requestsTab"; 
            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        public async Task<IActionResult> CreateDirect(int TechnicianId, string ServiceType, string Address, double? Latitude, double? Longitude)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            var clientName = HttpContext.Session.GetString("UserName") ?? "A client";

            if (clientId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(Address) || !Latitude.HasValue || !Longitude.HasValue)
            {
                TempData["Error"] = "íÌÈ ÊÍÏíÏ ãæÞÚ ÇáÎÏãÉ Úáì ÇáÎÑíØÉ ÈÏÞÉ.";
                return RedirectToAction("Index", "Offers");
            }

            var technician = await _context.Users.FindAsync(TechnicianId);
            if (technician == null || technician.Role != "Technician" || !technician.IsActive)
            {
                TempData["Error"] = "åÐÇ ÇáÝäí ÛíÑ ãÊÇÍ ÍÇáíÇð.";
                return RedirectToAction("Index", "Offers");
            }

            var request = new Request
            {
                ClientId = clientId.Value,
                TechnicianId = TechnicianId,
                ServiceType = ServiceType,
                Address = Address.Trim(),
                Latitude = Latitude,
                Longitude = Longitude,
                Status = "Pending",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Requests.Add(request);

            var user = await _context.Users.FindAsync(clientId.Value);
            if (user != null)
            {
                user.Address = Address.Trim();
                user.Latitude = Latitude;
                user.Longitude = Longitude;
                user.LocationUpdatedAt = DateTime.Now;
                _context.Update(user);
            }

            _context.Notifications.Add(new Notification
            {
                UserId = TechnicianId,
                Message = $"?? {clientName} sent you a direct request for {ServiceType}!",
                Link = "/TechnicianRequests/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Request sent successfully!";
            TempData["ActiveTab"] = "requestsTab"; 
            return RedirectToAction("Index", "Profile");
        }

        [HttpGet]
        public IActionResult GetAcceptedNotifications()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { hasNewAccepted = false });

            var hasUnread = _context.Notifications
                .Any(n => n.UserId == userId && !n.IsRead && n.Message.ToLower().Contains("accepted"));

            return Json(new { hasNewAccepted = hasUnread });
        }
    }
}
