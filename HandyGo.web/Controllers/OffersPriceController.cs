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
    public class OffersPriceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OffersPriceController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public IActionResult RequestOffers(int requestId)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var notifications = _context.Notifications
                .Where(n => n.UserId == clientId && !n.IsRead && n.Link.Contains("requestId=" + requestId))
                .ToList();

            if (notifications.Any())
            {
                notifications.ForEach(n => n.IsRead = true);
                _context.SaveChanges();
            }

            var request = _context.Requests
                .Include(r => r.Bids)
                .ThenInclude(b => b.Technician)
                .ThenInclude(u => u.ReceivedReviews)
                .FirstOrDefault(r => r.Id == requestId && r.ClientId == clientId);

            if (request == null) return NotFound();

            var client = _context.Users.Find(clientId);
            ViewBag.ClientWalletBalance = client?.WalletBalance ?? 0;

            return View(request);
        }

        [HttpPost]
        public IActionResult AcceptBid(int bidId)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var bid = _context.Bids
                .Include(b => b.Request)
                .FirstOrDefault(b => b.Id == bidId && b.Request.ClientId == clientId);

            if (bid == null) return NotFound();

            return RedirectToAction("Index", "Checkout", new
            {
                requestId = bid.RequestId,
                technicianId = bid.TechnicianId,
                price = bid.Price,
                bidId = bid.Id
            });
        }

        public IActionResult MyPublicRequests()
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var myRequests = _context.Requests
                .Include(r => r.Bids)
                .Where(r => r.ClientId == clientId && r.Status == "OpenForBids")
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(myRequests);
        }

        [HttpPost]
        public async Task<IActionResult> RejectBid(int bidId)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var bid = _context.Bids
                .Include(b => b.Request)
                .FirstOrDefault(b => b.Id == bidId && b.Request.ClientId == clientId);

            if (bid == null) return NotFound();

            int requestId = bid.RequestId;

            var notification = new Notification
            {
                UserId = bid.TechnicianId,
                Message = $"?  „ —›÷ «·⁄—÷ «·–Ì ﬁœ„ Â ·ÿ·» {bid.Request.ServiceType}.",
                Link = "/TechnicianRequests/OpenJobs",
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);

            _context.Bids.Remove(bid);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ —›÷ «·⁄—÷ »‰Ã«Õ.";
            return RedirectToAction("RequestOffers", new { requestId = requestId });
        }

        [HttpPost]
        public IActionResult CancelRequest(int requestId)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .Include(r => r.Bids)
                .FirstOrDefault(r => r.Id == requestId && r.ClientId == clientId);

            if (request == null) return NotFound();

            _context.Bids.RemoveRange(request.Bids);
            _context.Requests.Remove(request);
            _context.SaveChanges();

            TempData["Success"] = " „ ≈·€«¡ «·ÿ·».";
            return RedirectToAction("MyPublicRequests");
        }



        [HttpPost]
        public async Task<IActionResult> ConfirmServiceCompletion(int requestId)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests
                .Include(r => r.Technician)
                .FirstOrDefault(r => r.Id == requestId && r.ClientId == clientId);

            if (request == null) return NotFound();

            if (request.Status != "PendingClientConfirmation")
            {
                TempData["Error"] = "Â–« «·ÿ·» ·Ì” Ã«Â“« ·· √ﬂÌœ »⁄œ.";
                return RedirectToAction("Index", "Profile");
            }

            request.Status = "Completed";
            request.UpdatedAt = DateTime.Now;



            decimal totalPrice = request.Price ?? 0;

            decimal platformCommission = request.PlatformCommission ?? (totalPrice * 0.10m);
            decimal netToTechnician = request.NetToTechnician ?? (totalPrice * 0.90m);

            if (request.PaymentStatus == "InEscrow")
            {
                request.PaymentStatus = "Released";

                request.Technician.WalletBalance += netToTechnician;

                _context.Notifications.Add(new Notification
                {
                    UserId = request.TechnicianId.Value,
                    Message = $"?? «·⁄„Ì· √ﬂœ «·«” ·«„ (œ›⁄ ≈·ﬂ —Ê‰Ì/„Õ›Ÿ…).  „  ≈÷«›… √—»«Õﬂ ({netToTechnician} Ã.„) ·„Õ›Ÿ ﬂ.",
                    Link = "/Wallet/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            else if (request.PaymentStatus == "PendingCash")
            {
                request.PaymentStatus = "ReleasedCash";



                request.Technician.WalletBalance -= platformCommission;

                string commissionMsg = platformCommission < 0
                    ? $" „  ⁄ÊÌ÷ﬂ »„»·€ ({Math.Abs(platformCommission)} Ã.„) ›Ì „Õ›Ÿ ﬂ ‰Ÿ—« ·«” Œœ«„ «·⁄„Ì· ·ﬂÊ»Ê‰ Œ’„."
                    : $" „ Œ’„ ⁄„Ê·… «·„‰’… ({platformCommission} Ã.„) „‰ „Õ›Ÿ ﬂ ﬂ„œÌÊ‰Ì….";

                _context.Notifications.Add(new Notification
                {
                    UserId = request.TechnicianId.Value,
                    Message = $"?? «·⁄„Ì· √ﬂœ «·«” ·«„ (œ›⁄ ﬂ«‘). {commissionMsg}",
                    Link = "/Wallet/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }


            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „  √ﬂÌœ «·«” ·«„ »‰Ã«Õ. ‘ﬂ—« ·Àﬁ ﬂ!";
            TempData["ActiveTab"] = "requestsTab";
            return RedirectToAction("Index", "Profile");
        }
    }
}
