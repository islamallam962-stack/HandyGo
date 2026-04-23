using HandyGo.web.Data;
using HandyGo.web.Models;
using HandyGo.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using HandyGo.web.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HandyGo.web.Controllers
{
    public class OffersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OffersController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }



        public async Task<IActionResult> Index(string sort = "distance", double? lat = null, double? lng = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Index", "Profile");

            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null || !currentUser.IsActive) return RedirectToAction("Login", "Account");

            double clientLat = lat ?? currentUser.Latitude ?? 0;
            double clientLng = lng ?? currentUser.Longitude ?? 0;

            ViewBag.CurrentUserLat = clientLat;
            ViewBag.CurrentUserLng = clientLng;
            ViewBag.CurrentSort = sort;

            var activeThreshold = DateTime.Now.AddHours(-24);

            List<TechnicianOffersViewModel> GetTechsByCategory(string category)
            {

                var techs = _context.Users
                    .Where(u => u.Role == "Technician" && u.IsActive && u.LastSeen >= activeThreshold && u.Category == category)
                    .Select(u => new TechnicianOffersViewModel
                    {
                        Technician = u,
                        AverageRating = _context.Reviews.Any(r => r.TechnicianId == u.Id)
                                        ? _context.Reviews.Where(r => r.TechnicianId == u.Id).Average(r => r.Stars) : 0,
                        ReviewCount = _context.Reviews.Count(r => r.TechnicianId == u.Id)
                    }).ToList();

                if (clientLat != 0 && clientLng != 0)
                {
                    foreach (var tech in techs)
                    {
                        if (tech.Technician.Latitude.HasValue && tech.Technician.Longitude.HasValue)
                            tech.Distance = CalculateDistance(clientLat, clientLng, tech.Technician.Latitude.Value, tech.Technician.Longitude.Value);
                        else
                            tech.Distance = 9999;
                    }

                    var nearbyTechs = techs.Where(t => t.Distance <= 100).ToList();

                    if (sort == "rating")
                        return nearbyTechs.OrderByDescending(t => t.AverageRating).ThenBy(t => t.Distance).ToList();
                    else 
                        return nearbyTechs.OrderBy(t => t.Distance).ToList();
                }

                return new List<TechnicianOffersViewModel>();
            }

            ViewBag.Plumbers = GetTechsByCategory("Plumber");
            ViewBag.Electricians = GetTechsByCategory("Electrician");
            ViewBag.AC = GetTechsByCategory("AC");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RequestService(int technicianId, string Address, double? Latitude, double? Longitude)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(Address) || !Latitude.HasValue || Latitude == 0 || !Longitude.HasValue || Longitude == 0)
            {
                TempData["Error"] = "ÌÃ»  ÕœÌœ „Êﬁ⁄ «·Œœ„… ⁄·Ï «·Œ—Ìÿ… »œﬁ….";
                return RedirectToAction("Index", "Offers");
            }

            var client = await _context.Users.FindAsync(clientId.Value);
            if (client == null || !client.IsActive) return RedirectToAction("Login", "Account");

            var technician = await _context.Users.FindAsync(technicianId);

            if (technician == null || technician.Role != "Technician" || !technician.IsActive)
            {
                TempData["Error"] = "Â–« «·›‰Ì €Ì— „ «Õ Õ«·Ì«.";
                return RedirectToAction("Index", "Offers");
            }

            var request = new Request
            {
                ClientId = clientId.Value,
                TechnicianId = technicianId,
                ServiceType = technician.Category,
                Address = Address.Trim(),
                Latitude = Latitude,
                Longitude = Longitude,
                Status = "Pending",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Requests.Add(request);

            client.Address = Address.Trim();
            client.Latitude = Latitude;
            client.Longitude = Longitude;
            client.LocationUpdatedAt = DateTime.Now;
            _context.Update(client);

            _context.Notifications.Add(new Notification
            {
                UserId = technicianId,
                Message = $"{client.Name ?? "⁄„Ì·"} ÿ·» Œœ„ ﬂ «·„»«‘—… ›Ì {Address}° Ì„ﬂ‰ﬂ «·¬‰  ﬁœÌ„ ⁄—÷ ”⁄—.",
                Link = "/TechnicianRequests/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = $" „ ≈—”«· ÿ·»ﬂ »‰Ã«Õ ≈·Ï {technician.Name}. ›Ì «‰ Ÿ«— ≈—”«· ⁄—÷ «·”⁄— „‰Â ··»œ¡.";
            TempData["ActiveTab"] = "requestsTab"; 

            return RedirectToAction("Index", "Profile");
        }



        public async Task<IActionResult> LiveMap(double? lat = null, double? lng = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            var client = await _context.Users.FindAsync(userId);

            double clientLat = lat ?? client?.Latitude ?? 30.0444;
            double clientLng = lng ?? client?.Longitude ?? 31.2357;

            var activeThreshold = DateTime.Now.AddHours(-24);

            var allTechs = _context.Users
                .Where(u => u.Role == "Technician" && u.IsActive && u.LastSeen >= activeThreshold && u.Latitude != null && u.Longitude != null)
                .ToList();

            var nearbyTechs = allTechs
                .Where(u => CalculateDistance(clientLat, clientLng, u.Latitude.Value, u.Longitude.Value) <= 100)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.Name,
                    category = u.Category,
                    lat = u.Latitude,
                    lng = u.Longitude,
                    image = u.ImagePath ?? "/images/default.png",
                    distance = Math.Round(CalculateDistance(clientLat, clientLng, u.Latitude.Value, u.Longitude.Value), 1)
                }).ToList();

            ViewBag.ClientLat = clientLat;
            ViewBag.ClientLng = clientLng;
            ViewBag.TechsJson = System.Text.Json.JsonSerializer.Serialize(nearbyTechs);
            ViewBag.TechsCount = nearbyTechs.Count;

            return View();
        }



        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; 
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 2);
        }

        private double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
