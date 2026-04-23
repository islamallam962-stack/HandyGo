using HandyGo.web.Data;
using HandyGo.web.Models;
using HandyGo.web.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class TechnicianRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TechnicianRequestsController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserRole") != "Technician")
                return RedirectToAction("Index", "Profile");

            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null)
                return RedirectToAction("Login", "Account");

            var requests = _context.Requests
                .Include(r => r.Client)
                .Where(r => r.TechnicianId == techId && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(requests);
        }



        [HttpPost]
        public async Task<IActionResult> Accept(int id, decimal price, string estimatedTime)
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests.FirstOrDefault(r => r.Id == id && r.TechnicianId == techId);
            if (request == null)
            {
                TempData["Error"] = "«·ÿ·» €Ì— „ÊÃÊœ √Ê €Ì— „’—Õ ·ﬂ »«·Ê’Ê· ≈·ÌÂ.";
                return RedirectToAction("Index");
            }

            if (price <= 0)
            {
                TempData["Error"] = "ÌÃ»  ÕœÌœ ”⁄— „»œ∆Ì ·≈—”«· «·⁄—÷ ··⁄„Ì·.";
                return RedirectToAction("Index");
            }

            var techName = HttpContext.Session.GetString("UserName") ?? "«·›‰Ì";

            var bid = new Bid
            {
                RequestId = id,
                TechnicianId = techId.Value,
                Price = price,
                EstimatedTime = estimatedTime ?? "€Ì— „Õœœ",
                Note = " „ ≈—”«· Â–« «·⁄—÷ »‰«¡ ⁄·Ï ÿ·»ﬂ «·„»«‘—. Ì„ﬂ‰ﬂ „—«”· Ì ·· ›«Ê÷ √Ê ﬁ»Ê· «·⁄—÷ ··»œ¡.",
                CreatedAt = DateTime.Now
            };
            _context.Bids.Add(bid);

            request.Status = "WaitingForClientAcceptance";
            request.UpdatedAt = DateTime.Now;

            _context.Notifications.Add(new Notification
            {
                UserId = request.ClientId,
                Message = $"?? {techName} √—”· ·ﬂ ⁄—÷ ”⁄— ({price} Ã.„) ·ÿ·»ﬂ. Ì„ﬂ‰ﬂ ﬁ»Ê·Â ··œ›⁄ √Ê „—«”· Â ·· ›«Ê÷.",
                Link = $"/OffersPrice/RequestOffers?requestId={id}",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ ≈—”«· ⁄—÷ «·”⁄— »‰Ã«Õ! «·Œœ„… ·‰  »œ√ Õ Ï ÌÊ«›ﬁ «·⁄„Ì· ÊÌœ›⁄.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests.FirstOrDefault(r => r.Id == id && r.TechnicianId == techId);
            if (request == null)
            {
                TempData["Error"] = "Request not found or unauthorized.";
                return RedirectToAction("Index");
            }

            request.Status = "Rejected";
            request.UpdatedAt = DateTime.Now;

            _context.Notifications.Add(new Notification
            {
                UserId = request.ClientId,
                Message = "? Your service request was declined by the technician.",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Request has been declined.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> StartService(int id)
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests.FirstOrDefault(r => r.Id == id && r.TechnicianId == techId);
            if (request == null) return NotFound();

            request.Status = "InProgress";
            request.ActualStartTime = DateTime.Now;
            request.UpdatedAt = DateTime.Now;

            _context.Notifications.Add(new Notification
            {
                UserId = request.ClientId,
                Message = "??? The technician has arrived and started the service.",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Service started successfully!";
            TempData["ActiveTab"] = "requestsTab"; 
            return RedirectToAction("Index", "Profile");
        }



        [HttpPost]
        public async Task<IActionResult> FinishService(int id, bool completed)
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .Include(r => r.Technician)
                .FirstOrDefault(r => r.Id == id && r.TechnicianId == techId);

            if (request == null) return NotFound();

            if (completed)
            {

                request.Status = "PendingClientConfirmation";
                request.UpdatedAt = DateTime.Now;

                _context.Notifications.Add(new Notification
                {
                    UserId = request.ClientId,
                    Message = "?? «·›‰Ì √»·€ »√‰Â √‰ÂÏ «·⁄„·. Ì—ÃÏ „—«Ã⁄… «·Œœ„… Ê«·÷€ÿ ⁄·Ï ' √ﬂÌœ «·«” ·«„' · ÕÊÌ· √ ⁄«»Â.",
                    Link = "/Profile/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

                TempData["Success"] = " „ ≈—”«· ÿ·» ≈‰Â«¡ «·Œœ„… ··⁄„Ì·. ”  „ ≈÷«›… «·√—»«Õ ·„Õ›Ÿ ﬂ ›Ê—  √ﬂÌœÂ ··«” ·«„.";
            }
            else
            {
                request.Status = "NotCompleted";
                request.UpdatedAt = DateTime.Now;

                _context.Notifications.Add(new Notification
                {
                    UserId = request.ClientId,
                    Message = "?? The service session has ended without completion.",
                    Link = "/Profile/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

                TempData["Success"] = " „ ≈‰Â«¡ «·Ã·”… »œÊ‰ ≈ﬂ„«· «·⁄„·.";
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["ActiveTab"] = "requestsTab"; 
            return RedirectToAction("Index", "Profile");
        }

        public IActionResult OpenJobs()
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null || HttpContext.Session.GetString("UserRole") != "Technician")
                return RedirectToAction("Login", "Account");

            var tech = _context.Users.Find(techId);

            var submittedBidRequestIds = _context.Bids
                .Where(b => b.TechnicianId == techId)
                .Select(b => b.RequestId)
                .ToList();

            var openRequests = _context.Requests
                .Include(r => r.Client)
                .Where(r => r.TechnicianId == null &&
                            r.ServiceType == tech.Category &&
                            r.Status == "OpenForBids" &&
                            !submittedBidRequestIds.Contains(r.Id))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(openRequests);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBid(int RequestId, decimal Price, string Note, string EstimatedTime)
        {
            var techId = HttpContext.Session.GetInt32("UserId");
            if (techId == null || HttpContext.Session.GetString("UserRole") != "Technician")
                return RedirectToAction("Login", "Account");

            if (Price <= 0 || string.IsNullOrEmpty(EstimatedTime))
            {
                TempData["Error"] = "Please provide a valid price and estimated time.";
                return RedirectToAction("OpenJobs");
            }

            var request = _context.Requests.FirstOrDefault(r => r.Id == RequestId && r.Status == "OpenForBids");
            if (request == null)
            {
                TempData["Error"] = "This request is no longer available for bids.";
                return RedirectToAction("OpenJobs");
            }

            try
            {
                var bid = new Bid
                {
                    RequestId = RequestId,
                    TechnicianId = techId.Value,
                    Price = Price,
                    Note = Note ?? "",
                    EstimatedTime = EstimatedTime,
                    CreatedAt = DateTime.Now
                };

                _context.Bids.Add(bid);

                _context.Notifications.Add(new Notification
                {
                    UserId = request.ClientId,
                    Message = $"?? New offer received: {Price} EGP for your {request.ServiceType} request.",
                    Link = "/OffersPrice/RequestOffers?requestId=" + RequestId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNotification");

                TempData["Success"] = "Your offer has been sent successfully!";
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = "Database Error: " + innerMsg;
            }

            return RedirectToAction("OpenJobs");
        }

        [HttpGet]
        public IActionResult GetPendingRequestsCount()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(0);

            var count = _context.Requests
                .Count(r => r.TechnicianId == userId && r.Status == "Pending");

            return Json(count);
        }
    }
}
