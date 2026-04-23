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
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public ProfileController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");


            var user = _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == userId);

            if (user == null || !user.IsActive)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            if (!string.IsNullOrEmpty(user.ReferralCode))
            {
                HttpContext.Session.SetString("UserReferralCode", user.ReferralCode);
            }

            var requests = _context.Requests
                .Include(r => r.Client)
                .Include(r => r.Technician)
                .Include(r => r.Review)
                .Where(r => r.ClientId == user.Id || r.TechnicianId == user.Id)
                .AsNoTracking() 
                .ToList() 
                .OrderByDescending(r => r.UpdatedAt)
                .OrderByDescending(r => r.Status == "InProgress")
                .ThenByDescending(r => r.Status == "Accepted")
                .ThenByDescending(r => r.Status == "Pending")
                .ToList();

            ViewBag.Requests = requests;

            return View(user);
        }

        public IActionResult Details(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var currentUserRole = HttpContext.Session.GetString("UserRole");

            if (currentUserId == null) return RedirectToAction("Login", "Account");

            if (id == currentUserId) return RedirectToAction("Index");

            var requestedUser = _context.Users.Find(id);
            if (requestedUser == null) return NotFound("User not found.");

            if (currentUserRole == "Admin") return View(requestedUser);

            if (currentUserRole == "Client" && requestedUser.Role == "Technician")
            {
                return View(requestedUser);
            }


            bool hasConnection = _context.Requests.Any(r =>
                (r.ClientId == currentUserId && r.TechnicianId == id) ||
                (r.TechnicianId == currentUserId && r.ClientId == id));

            if (hasConnection)
            {
                return View(requestedUser);
            }

            TempData["Error"] = "⁄–—«° €Ì— „’—Õ ·ﬂ »«·Ê’Ê· ·Â–« «·„·› «·‘Œ’Ì.";
            return RedirectToAction("Index", "Profile");
        }

        [HttpGet]
        public IActionResult Edit()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(User model, IFormFile imageFile)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            if (model.Id != 0 && model.Id != userId)
            {
                return Unauthorized("Action not allowed.");
            }

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            user.Name = model.Name?.Trim();
            user.Phone = model.Phone?.Trim();

            if (user.Role == "Technician")
            {
                user.Skills = model.Skills?.Trim();
                user.Certificates = model.Certificates?.Trim();
            }

            if (imageFile != null && imageFile.Length > 0)
            {

                if (imageFile.Length > 2 * 1024 * 1024)
                {
                    TempData["Error"] = "Image size cannot exceed 2MB.";
                    return RedirectToAction("Edit", "Profile");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] = "Invalid image format. Only JPG, PNG, and GIF are allowed.";
                    return RedirectToAction("Edit", "Profile");
                }

                if (!string.IsNullOrEmpty(user.ImagePath))
                {
                    var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = $"user_{user.Id}_{DateTime.Now.Ticks}{extension}";
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profiles", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    imageFile.CopyTo(stream);
                }

                user.ImagePath = "/images/profiles/" + fileName;
            }

            _context.SaveChanges();
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int RequestId, int TechId, int Stars, string Comment)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            if (Stars < 1 || Stars > 5)
            {
                TempData["Error"] = "Invalid rating value.";
                return RedirectToAction("Index", "Profile");
            }

            var request = _context.Requests.FirstOrDefault(r => r.Id == RequestId && r.ClientId == clientId && r.TechnicianId == TechId);
            if (request == null) return NotFound("Request not found or you are not authorized to review this.");

            if (request.Status != "Completed")
            {
                TempData["Error"] = "You can only review completed services.";
                return RedirectToAction("Index", "Profile");
            }

            var alreadyRated = _context.Reviews.Any(rev => rev.RequestId == RequestId);
            if (alreadyRated)
            {
                TempData["Error"] = "You have already rated this service.";
                return RedirectToAction("Index", "Profile");
            }

            var review = new Review
            {
                RequestId = RequestId,
                TechnicianId = TechId,
                Stars = Stars,
                Comment = Comment?.Trim() ?? "",
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);

            _context.Notifications.Add(new Notification
            {
                UserId = TechId,
                Message = $"? You received a new {Stars}-star review!",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Review submitted successfully!";
            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int requestId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var clientName = HttpContext.Session.GetString("UserName") ?? "The client";
            if (userId == null) return RedirectToAction("Login", "Account");

            var request = _context.Requests.FirstOrDefault(r => r.Id == requestId && r.ClientId == userId);
            if (request == null) return NotFound("Request not found or unauthorized.");

            if (request.Status == "Completed" || request.Status == "Closed")
            {
                TempData["Error"] = "Cannot cancel a completed request.";
                return RedirectToAction("Index", "Profile");
            }

            if (request.TechnicianId != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = (int)request.TechnicianId,
                    Message = $"?? Sorry, {clientName} has cancelled their {request.ServiceType} request.",
                    Link = "#",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            var messages = _context.Messages.Where(m => m.RequestId == requestId).ToList();
            _context.Messages.RemoveRange(messages);
            _context.Requests.Remove(request);

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Request cancelled and technician has been notified.";
            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitComplaint(int requestId, string subject, string description)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null) return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description))
            {
                return Json(new { success = false, message = "Subject and description are required." });
            }

            var request = _context.Requests.FirstOrDefault(r => r.Id == requestId && r.ClientId == userId);
            if (request != null)
            {
                var complaint = new Complaint
                {
                    RequestId = requestId,
                    Subject = subject.Trim(),
                    Description = description.Trim(),
                    CreatedAt = DateTime.Now,
                    IsResolved = false
                };
                _context.Complaints.Add(complaint);
                _context.SaveChanges();

                return Json(new { success = true, message = "Complaint sent to Admin." });
            }
            return Json(new { success = false, message = "Request not found or unauthorized." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitReport(int reportedUserId, string reason)
        {
            var reporterId = HttpContext.Session.GetInt32("UserId");
            if (reporterId == null) return RedirectToAction("Login", "Account");
            if (reporterId == reportedUserId) return BadRequest("You cannot report yourself.");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Report reason is required.";
                return RedirectToAction("Details", "Profile", new { id = reportedUserId });
            }

            var reportedUser = _context.Users.Find(reportedUserId);
            if (reportedUser == null) return NotFound("User not found.");

            var report = new UserReport
            {
                ReporterId = (int)reporterId,
                ReportedUserId = reportedUserId,
                Reason = reason.Trim(),
                CreatedAt = DateTime.Now
            };
            _context.UserReports.Add(report);
            _context.SaveChanges();

            TempData["Success"] = "Your report has been submitted to Admin.";
            return RedirectToAction("Details", "Profile", new { id = reportedUserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteImage()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            if (user == null || string.IsNullOrEmpty(user.ImagePath)) return RedirectToAction("Edit", "Profile");

            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ImagePath.TrimStart('/'));

            if (System.IO.File.Exists(imagePath) && imagePath.StartsWith(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles")))
            {
                System.IO.File.Delete(imagePath);
            }

            user.ImagePath = null;
            _context.SaveChanges();
            return RedirectToAction("Edit", "Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearAdminNote()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.AdminNote = null;
                _context.SaveChanges();
            }
            return RedirectToAction("Index", "Profile");
        }

        [HttpGet]
        public IActionResult UpgradeToTechnician()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            if (user.Role == "Technician") return RedirectToAction("Index");

            var model = new UpgradeToTechnicianViewModel
            {
                Phone = user.Phone
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpgradeToTechnician(UpgradeToTechnicianViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid) return View(model);

            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.Role = "Technician";
                user.Category = model.Category;
                user.Phone = model.Phone?.Trim();
                user.Skills = model.Skills?.Trim();
                user.Certificates = model.Certificates?.Trim();
                user.Address = model.Address?.Trim();
                user.Latitude = model.Latitude;
                user.Longitude = model.Longitude;

                _context.SaveChanges();

                HttpContext.Session.SetString("UserRole", "Technician");

                TempData["Success"] = "„—Õ»« »ﬂ ›Ì ›—Ìﬁ «·›‰ÌÌ‰! Ì„ﬂ‰ﬂ «·¬‰ «” ﬁ»«· «·ÿ·»« .";
                return RedirectToAction("Index");
            }

            return View(model);
        }



        [HttpPost]
        public async Task<IActionResult> SubmitVerification(List<IFormFile> idCards, IFormFile certificate)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null || user.Role != "Technician") return RedirectToAction("Index");

            if (idCards == null || idCards.Count == 0 || idCards.Count > 5)
            {
                TempData["Error"] = "ÌÃ» ≈—›«ﬁ „‰ ’Ê—… Ê«Õœ… ≈·Ï 5 ’Ê— ﬂÕœ √ﬁ’Ï ··»ÿ«ﬁ….";
                return RedirectToAction("Index");
            }

            long maxFileSize = 5 * 1024 * 1024; 

            foreach (var file in idCards)
            {
                if (file.Length > maxFileSize)
                {
                    TempData["Error"] = $"«·’Ê—… '{file.FileName}'   ŒÿÏ «·Õœ «·√ﬁ’Ï ··ÕÃ„ (5 „ÌÃ«»«Ì ).";
                    return RedirectToAction("Index");
                }
            }

            if (certificate != null && certificate.Length > maxFileSize)
            {
                TempData["Error"] = "’Ê—… «·›Ì‘ «·Ã‰«∆Ì   ŒÿÏ «·Õœ «·√ﬁ’Ï ··ÕÃ„ (5 „ÌÃ«»«Ì ).";
                return RedirectToAction("Index");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "verifications");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            List<string> savedIdCardPaths = new List<string>();
            foreach (var file in idCards)
            {
                var fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                savedIdCardPaths.Add("/uploads/verifications/" + fileName);
            }

            user.IdCardImage = string.Join(",", savedIdCardPaths);

            if (certificate != null && certificate.Length > 0)
            {
                var certFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(certificate.FileName);
                var certPath = Path.Combine(uploadsFolder, certFileName);
                using (var stream = new FileStream(certPath, FileMode.Create))
                {
                    await certificate.CopyToAsync(stream);
                }
                user.CertificateImage = "/uploads/verifications/" + certFileName;
            }

            user.VerificationStatus = "Pending";
            user.VerificationRejectionReason = null;

            _context.Notifications.Add(new Notification
            {
                UserId = 1,
                Message = $"?? ÿ·»  ÊÀÌﬁ ÃœÌœ „‰ «·›‰Ì: {user.Name}.  „ ≈—›«ﬁ {idCards.Count} ’Ê— »ÿ«ﬁ… Ê«·›Ì‘ «·Ã‰«∆Ì.",
                Link = "/Admin/Verifications",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = " „ —›⁄ √Ê—«ﬁﬂ »‰Ã«Õ! ”Ì „ „—«Ã⁄ Â« „‰ ﬁ»· «·≈œ«—… ﬁ—Ì»« · ›⁄Ì· ‘«—… «· ÊÀÌﬁ.";
            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> RaiseFinancialDispute(int requestId, string reason, string details)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var request = await _context.Requests.FindAsync(requestId);

            if (request == null || (request.ClientId != userId && request.TechnicianId != userId))
                return NotFound();

            if (request.PaymentStatus == "InEscrow")
            {
                request.PaymentStatus = "Disputed"; 
            }

            var dispute = new FinancialDispute
            {
                RequestId = requestId,
                InitiatorUserId = userId.Value,
                Reason = reason,
                Details = details
            };

            _context.FinancialDisputes.Add(dispute);

            _context.Notifications.Add(new Notification
            {
                UserId = 1, 
                Message = $"?? ‰“«⁄ „«·Ì ÃœÌœ »Œ’Ê’ «·ÿ·» —ﬁ„ #{requestId}",
                Link = "/Admin/FinancialDisputes",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ › Õ «·‰“«⁄ Ê Ã„Ìœ «·√„Ê«· »‰Ã«Õ. «·≈œ«—… ”  Ê«’· „⁄ﬂ.";

            if (HttpContext.Session.GetString("UserRole") == "Technician")
                return RedirectToAction("Index", "TechnicianRequests");
            else
                return RedirectToAction("Index", "Profile");
        }
    }
}
