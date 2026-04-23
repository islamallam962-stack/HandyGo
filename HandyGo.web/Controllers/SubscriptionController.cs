using HandyGo.web.Data;
using HandyGo.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly AppDbContext _context;

        public SubscriptionController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            if (user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry.Value < DateTime.Now)
            {
                user.SubscriptionPlan = null;
                user.SubscriptionExpiry = null;
                _context.SaveChanges();
            }

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe(string planCode, decimal price, int durationMonths)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.WalletBalance < price)
            {
                TempData["Error"] = $"Insufficient wallet balance. You need {price} EGP to subscribe to this plan.";
                return RedirectToAction("Index");
            }

            user.WalletBalance -= price;
            user.SubscriptionPlan = planCode;

            if (user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry.Value > DateTime.Now)
                user.SubscriptionExpiry = user.SubscriptionExpiry.Value.AddMonths(durationMonths);
            else
                user.SubscriptionExpiry = DateTime.Now.AddMonths(durationMonths);

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = $"?? Congratulations! Your ({planCode}) plan has been activated successfully. Valid until {user.SubscriptionExpiry.Value.ToString("yyyy-MM-dd")}.",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Premium plan subscription completed successfully!";
            return RedirectToAction("Index");
        }
    }
}
