using HandyGo.web.Data;
using HandyGo.web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using HandyGo.web.Hubs;

namespace HandyGo.web.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AdminController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        public IActionResult Users()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var now = DateTime.Now;
            var allRequests = _context.Requests.ToList();

            ViewBag.Stats = new Dictionary<string, int>
            {
                { "TodayTotal", allRequests.Count(r => r.CreatedAt.Date == now.Date) },
                { "TodayDone", allRequests.Count(r => r.CreatedAt.Date == now.Date && r.Status == "Completed") },
                { "WeekTotal", allRequests.Count(r => r.CreatedAt >= now.AddDays(-7)) },
                { "WeekDone", allRequests.Count(r => r.CreatedAt >= now.AddDays(-7) && r.Status == "Completed") },
                { "MonthTotal", allRequests.Count(r => r.CreatedAt >= now.AddMonths(-1)) },
                { "MonthDone", allRequests.Count(r => r.CreatedAt >= now.AddMonths(-1) && r.Status == "Completed") },
                { "YearTotal", allRequests.Count(r => r.CreatedAt >= now.AddYears(-1)) },
                { "YearDone", allRequests.Count(r => r.CreatedAt >= now.AddYears(-1) && r.Status == "Completed") }
            };

            var totalCount = allRequests.Count();
            var doneCount = allRequests.Count(r => r.Status == "Completed");
            ViewBag.SuccessRate = totalCount > 0 ? (int)((double)doneCount / totalCount * 100) : 0;

            ViewBag.TopTechnicians = _context.Users
                .Where(u => u.Role == "Technician")
                .Select(u => new {
                    u.Name,
                    u.Category,
                    RequestCount = u.TechnicianRequests.Count()
                })
                .OrderByDescending(t => t.RequestCount)
                .Take(3)
                .ToList();

            var users = _context.Users
                .Include(u => u.ClientRequests)
                .Include(u => u.TechnicianRequests)
                .Include(u => u.ReceivedReviews)
                .ToList();

            ViewBag.NegativeReviews = _context.Reviews
                .Include(r => r.Request).ThenInclude(req => req.Client)
                .Include(r => r.Request).ThenInclude(req => req.Technician)
                .Where(r => r.Stars <= 2).OrderByDescending(r => r.CreatedAt).ToList();

            ViewBag.OfficialComplaints = _context.Complaints
                .Include(c => c.Request).ThenInclude(req => req.Client)
                .Include(c => c.Request).ThenInclude(req => req.Technician)
                .Where(c => !c.IsResolved).OrderByDescending(c => c.CreatedAt).ToList();

            ViewBag.UserReports = _context.UserReports
                .Include(r => r.Reporter)
                .Include(r => r.ReportedUser)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> SendGlobalNotification(string message)
        {
            if (!IsAdmin()) return Unauthorized();

            if (string.IsNullOrEmpty(message))
            {
                TempData["Error"] = "Message cannot be empty!";
                return RedirectToAction("Users");
            }

            var allUsers = _context.Users.ToList();
            var notifications = allUsers.Select(u => new Notification
            {
                UserId = u.Id,
                Message = $"?? [Announcement]: {message}",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Global notification sent to everyone!";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> SendWarning(int id, string warningMessage)
        {
            if (!IsAdmin()) return Unauthorized();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            user.AdminNote = warningMessage;
            user.AdminNoteDate = DateTime.Now; 

            var notification = new Notification
            {
                UserId = id,
                Message = "?? [System Warning]: Please check your profile for an urgent message from Admin.",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = "Warning sent successfully.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (!IsAdmin()) return Unauthorized();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;

            if (user.IsActive)
            {
                var notification = new Notification
                {
                    UserId = id,
                    Message = "? Good news! Your account has been re-activated by the Admin.",
                    Link = "/Profile/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = user.IsActive ? "Account activated." : "Account banned.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ResolveComplaint(int id)
        {
            if (!IsAdmin()) return Unauthorized();

            var complaint = _context.Complaints.Include(c => c.Request).FirstOrDefault(c => c.Id == id);
            if (complaint != null)
            {
                complaint.IsResolved = true;

                var notification = new Notification
                {
                    UserId = complaint.Request.ClientId,
                    Message = "??? Your complaint has been marked as Resolved by Admin.",
                    Link = "/Profile/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNotification");

                TempData["Success"] = "Complaint marked as resolved.";
            }
            return RedirectToAction("Users");
        }

        public IActionResult BlockUser(int id) => RedirectToAction("ToggleUserStatus", new { id = id });
        public IActionResult UnblockUser(int id) => RedirectToAction("ToggleUserStatus", new { id = id });

        public IActionResult DeleteUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["Success"] = "User deleted successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public IActionResult ToggleTopRated(int id)
        {
            if (!IsAdmin()) return Unauthorized();

            var tech = _context.Users.Find(id);
            if (tech != null)
            {
                tech.IsTopRated = !tech.IsTopRated;
                _context.SaveChanges();
                TempData["Success"] = tech.IsTopRated ? "Technician marked as Top Rated." : "Top Rated status removed.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public IActionResult SubmitReport(int reportedUserId, string reason)
        {
            var reporterId = HttpContext.Session.GetInt32("UserId");
            if (reporterId == null) return RedirectToAction("Login", "Account");

            var report = new UserReport
            {
                ReporterId = (int)reporterId,
                ReportedUserId = reportedUserId,
                Reason = reason,
                CreatedAt = DateTime.Now
            };
            _context.UserReports.Add(report);

            if (_context.SaveChanges() > 0)
                TempData["Success"] = "Report submitted successfully.";

            return RedirectToAction("Users"); 
        }

        [HttpPost]
        public async Task<IActionResult> TakeAction(int techId, string message)
        {
            if (!IsAdmin()) return Unauthorized();

            var user = await _context.Users.FindAsync(techId);
            if (user == null) return NotFound();

            user.AdminNote = message;
            user.AdminNoteDate = DateTime.Now; 

            var notification = new Notification
            {
                UserId = techId,
                Message = $"?? [ ‰»ÌÂ ≈œ«—Ì]: {message}",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ ≈—”«· «· Õ–Ì— ··›‰Ì »‰Ã«Õ.";

            string returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Users");
        }

        public IActionResult Complaints()
        {

            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var complaints = _context.Complaints
                .Include(c => c.Request)
                    .ThenInclude(r => r.Client)
                .Include(c => c.Request)
                    .ThenInclude(r => r.Technician)
                .ToList();

            return View(complaints);
        }

        [HttpPost]
        public IActionResult ResolveComplaint2(int id)
        {

            if (!IsAdmin()) return Unauthorized();

            var complaint = _context.Complaints
                .Include(c => c.Request)
                .FirstOrDefault(c => c.Id == id);

            if (complaint != null)
            {
                complaint.IsResolved = true; 

                var technician = _context.Users.Find(complaint.Request.TechnicianId);
                if (technician != null)
                {
                    technician.AdminNote = " „ Õ· «·‘ﬂÊÏ «·„ﬁœ„… ÷œﬂ »Œ’Ê’ «·ÿ·» —ﬁ„ " + complaint.RequestId;
                    technician.AdminNoteDate = DateTime.Now;
                }

                _context.SaveChanges();
                TempData["Success"] = "Complaint resolved and technician notified.";
            }
            return RedirectToAction("Complaints");
        }









        public IActionResult Financials()
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            var totalEarnings = _context.Requests
                .Where(r => r.Status == "Completed" && r.PlatformCommission != null)
                .Sum(r => r.PlatformCommission ?? 0);

            var escrowMoney = _context.Requests
                .Where(r => r.PaymentStatus == "InEscrow")
                .Sum(r => r.Price ?? 0);

            var techniciansBalance = _context.Users
                .Where(u => u.Role == "Technician")
                .Sum(u => u.WalletBalance);

            var clientsBalance = _context.Users
                .Where(u => u.Role == "Client")
                .Sum(u => u.WalletBalance);

            var totalPayouts = _context.WithdrawalRequests
                .Where(w => w.Status == "Approved")
                .Sum(w => w.Amount);

            var pendingWithdrawalsSum = _context.WithdrawalRequests
                .Where(w => w.Status == "Pending")
                .Sum(w => w.Amount);



            var actualBankBalance = totalEarnings + escrowMoney + techniciansBalance + clientsBalance + pendingWithdrawalsSum;

            ViewBag.TotalEarnings = totalEarnings;
            ViewBag.EscrowMoney = escrowMoney;
            ViewBag.TechniciansBalance = techniciansBalance;
            ViewBag.TotalPayouts = totalPayouts;
            ViewBag.ActualBankBalance = actualBankBalance; 

            var pendingWithdrawals = _context.WithdrawalRequests
                .Include(w => w.Technician)
                .Where(w => w.Status == "Pending")
                .OrderByDescending(w => w.CreatedAt)
                .ToList();

            return View(pendingWithdrawals);
        }



        [HttpPost]
        public async Task<IActionResult> ApproveWithdrawal(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var withdrawal = await _context.WithdrawalRequests
                .Include(w => w.Technician)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal == null || withdrawal.Status != "Pending") return NotFound();

            withdrawal.Status = "Approved";

            _context.Notifications.Add(new Notification
            {
                UserId = withdrawal.TechnicianId,
                Message = $"?? „»—Êﬂ!  „  «·„Ê«›ﬁ… ⁄·Ï ÿ·» «·”Õ» «·Œ«’ »ﬂ ({withdrawal.Amount} Ã.„) Ê „  ÕÊÌ· «·„»·€ ≈·Ìﬂ ⁄»— {withdrawal.Method}.",
                Link = "/Wallet/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „  √ﬂÌœ ⁄„·Ì… «·”Õ» »‰Ã«Õ Ê≈‘⁄«— «·›‰Ì.";
            return RedirectToAction("Financials");
        }



        [HttpPost]
        public async Task<IActionResult> RejectWithdrawal(int id, string reason)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var withdrawal = await _context.WithdrawalRequests
                .Include(w => w.Technician)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal == null || withdrawal.Status != "Pending") return NotFound();

            withdrawal.Status = "Rejected";

            withdrawal.Technician.WalletBalance += withdrawal.Amount;

            _context.Notifications.Add(new Notification
            {
                UserId = withdrawal.TechnicianId,
                Message = $"?  „ —›÷ ÿ·» «·”Õ» ({withdrawal.Amount} Ã.„) ·”»»: {reason}. Ê „  ≈⁄«œ… «·„»·€ ·„Õ›Ÿ ﬂ.",
                Link = "/Wallet/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ —›÷ «·ÿ·» Ê≈—Ã«⁄ «·„»·€ ·„Õ›Ÿ… «·›‰Ì.";
            return RedirectToAction("Financials");
        }



        public IActionResult Verifications(string status = "Pending")
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            var query = _context.Users
                .Where(u => u.Role == "Technician" && u.VerificationStatus != "Unverified");

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(u => u.VerificationStatus == status);
            }

            ViewBag.CurrentStatus = status;

            var verifications = query.OrderByDescending(u => u.CreatedAt).ToList();

            return View(verifications);
        }






        [HttpPost]
        public async Task<IActionResult> ApproveVerification(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role != "Technician") return NotFound();

            user.VerificationStatus = "Verified";
            user.VerificationRejectionReason = null;

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = "?? „»—Êﬂ!  „  „—«Ã⁄… √Ê—«ﬁﬂ Ê ÊÀÌﬁ Õ”«»ﬂ »‰Ã«Õ.",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync(); 




            if (user.ReferredByUserId != null && !string.IsNullOrEmpty(user.CertificateImage))
            {
                var referrer = await _context.Users.FindAsync(user.ReferredByUserId);
                if (referrer != null)
                {

                    int verifiedTechsCount = _context.Users.Count(u =>
                        u.ReferredByUserId == referrer.Id &&
                        u.Role == "Technician" &&
                        u.VerificationStatus == "Verified" &&
                        !string.IsNullOrEmpty(u.CertificateImage));

                    if (verifiedTechsCount > 0 && verifiedTechsCount % 5 == 0)
                    {
                        referrer.WalletBalance += 500; 

                        _context.Notifications.Add(new Notification
                        {
                            UserId = referrer.Id,
                            Message = $"?? „»—Êﬂ! ·ﬁœ √ﬂ„·  œ⁄Ê… {verifiedTechsCount} ›‰ÌÌ‰ „ÊÀﬁÌ‰ »‘Â«œ… Œ»—….  „  ≈÷«›… „ﬂ«›√… 500 Ã.„ ·„Õ›Ÿ ﬂ!",
                            Link = "/Wallet/Index",
                            CreatedAt = DateTime.Now,
                            IsRead = false
                        });

                        await _context.SaveChangesAsync();
                    }
                }
            }


            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = $" „  ÊÀÌﬁ Õ”«» Ê«⁄ „«œ «·›‰Ì {user.Name} »‰Ã«Õ.";
            return RedirectToAction("Verifications", new { status = "Pending" });
        }



        [HttpPost]
        public async Task<IActionResult> RejectVerification(int id, string reason)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role != "Technician") return NotFound();

            user.VerificationStatus = "Rejected";
            user.VerificationRejectionReason = reason;

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = $"?? ⁄–—«°  „ —›÷ √Ê—«ﬁ «· ÊÀÌﬁ «·Œ«’… »ﬂ ·”»»: {reason}. Ì—ÃÏ ≈⁄«œ… —›⁄ «·√Ê—«ﬁ «·’ÕÌÕ….",
                Link = "/Profile/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = $" „ —›÷ √Ê—«ﬁ {user.Name} Ê≈—”«· ≈‘⁄«— ·Â »«·”»».";
            return RedirectToAction("Verifications", new { status = "Pending" });
        }



        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            ViewBag.TotalClients = _context.Users.Count(u => u.Role == "Client");
            ViewBag.TotalTechnicians = _context.Users.Count(u => u.Role == "Technician");
            ViewBag.TotalCompletedRequests = _context.Requests.Count(r => r.Status == "Completed");

            ViewBag.TotalEarnings = _context.Requests
                .Where(r => r.Status == "Completed" && r.PlatformCommission != null)
                .Sum(r => r.PlatformCommission ?? 0);

            var techsByCategory = _context.Users
                .Where(u => u.Role == "Technician" && !string.IsNullOrEmpty(u.Category))
                .GroupBy(u => u.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.TechCategories = techsByCategory.Select(t => t.Category).ToList();
            ViewBag.TechCounts = techsByCategory.Select(t => t.Count).ToList();

            var requestsByStatus = _context.Requests
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.RequestStatuses = requestsByStatus.Select(r => r.Status).ToList();
            ViewBag.RequestCounts = requestsByStatus.Select(r => r.Count).ToList();

            return View();
        }



        public IActionResult PromoCodes()
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            var codes = _context.PromoCodes.OrderByDescending(p => p.Id).ToList();
            return View(codes);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePromoCode(PromoCode promo)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            promo.Code = promo.Code.ToUpper().Trim();

            if (_context.PromoCodes.Any(p => p.Code == promo.Code))
            {
                TempData["Error"] = "⁄›Ê«° Â–« «·ﬂÊœ „ÊÃÊœ »«·›⁄·!";
                return RedirectToAction("PromoCodes");
            }

            if (promo.ExpiryDate == default)
                promo.ExpiryDate = DateTime.Now.AddMonths(1);

            _context.PromoCodes.Add(promo);
            await _context.SaveChangesAsync();

            TempData["Success"] = $" „ ≈‰‘«¡ ﬂÊœ «·Œ’„ {promo.Code} »‰Ã«Õ!";
            return RedirectToAction("PromoCodes");
        }

        [HttpPost]
        public async Task<IActionResult> TogglePromoCode(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null) return NotFound();

            promo.IsActive = !promo.IsActive; 
            await _context.SaveChangesAsync();

            TempData["Success"] = $" „ {(promo.IsActive ? " ›⁄Ì·" : "≈Ìﬁ«›")} ﬂÊœ «·Œ’„ {promo.Code}.";
            return RedirectToAction("PromoCodes");
        }



        public IActionResult Affiliates()
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            var allUsers = _context.Users.ToList();

            var marketers = allUsers
                .Where(u => allUsers.Any(r => r.ReferredByUserId == u.Id))
                .OrderByDescending(u => allUsers.Count(r => r.ReferredByUserId == u.Id && r.VerificationStatus == "Verified"))
                .ToList();

            ViewBag.AllUsers = allUsers;

            return View(marketers);
        }



        public IActionResult FinancialDisputes()
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return RedirectToAction("Login", "Account");

            var disputes = _context.FinancialDisputes
                .Include(d => d.Request).ThenInclude(r => r.Technician)
                .Include(d => d.Request).ThenInclude(r => r.Client)
                .Include(d => d.InitiatorUser)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            return View(disputes);
        }

        [HttpPost]
        public async Task<IActionResult> ResolveFinancialDispute(int disputeId, string decision, string adminNote)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin") return Unauthorized();

            var dispute = await _context.FinancialDisputes
                .Include(d => d.Request).ThenInclude(r => r.Client)
                .Include(d => d.Request).ThenInclude(r => r.Technician)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null || dispute.Status != "Open") return NotFound();

            var req = dispute.Request;
            decimal totalPaidByClient = (req.NetToTechnician ?? 0) + (req.PlatformCommission ?? 0);

            if (decision == "RefundClient")
            {

                if (req.PaymentStatus == "Disputed" || req.PaymentStatus == "InEscrow")
                {
                    req.Client.WalletBalance += totalPaidByClient;
                    req.PaymentStatus = "Refunded";
                }
                req.Status = "Cancelled";
                dispute.Status = "RefundedToClient";

                _context.Notifications.Add(new Notification { UserId = req.ClientId, Message = "?  „ «·Õﬂ„ ·’«·Õﬂ ›Ì «·‰“«⁄ Ê≈—Ã«⁄ «·√„Ê«· ·„Õ›Ÿ ﬂ.", Link = "/Wallet/Index", CreatedAt = DateTime.Now });

                _context.Notifications.Add(new Notification { UserId = req.TechnicianId ?? 0, Message = $"?  „ ≈—Ã«⁄ √„Ê«· «·ÿ·» #{req.Id} ··⁄„Ì· »ﬁ—«— „‰ «·≈œ«—….", Link = "/TechnicianRequests/Index", CreatedAt = DateTime.Now });
            }
            else if (decision == "PayTechnician")
            {

                if (req.PaymentStatus == "Disputed" || req.PaymentStatus == "InEscrow")
                {
                    req.Technician.WalletBalance += req.NetToTechnician ?? 0;
                    req.PaymentStatus = "Released";
                }
                req.Status = "Completed";
                dispute.Status = "ReleasedToTech";

                _context.Notifications.Add(new Notification { UserId = req.TechnicianId ?? 0, Message = "?  „ «·Õﬂ„ ·’«·Õﬂ ›Ì «·‰“«⁄ Ê≈÷«›… «·√—»«Õ ·„Õ›Ÿ ﬂ.", Link = "/Wallet/Index", CreatedAt = DateTime.Now });
                _context.Notifications.Add(new Notification { UserId = req.ClientId, Message = $"??  „ «·›’· ›Ì «·‰“«⁄ ·’«·Õ «·›‰Ì Ê ÕÊÌ· «·√„Ê«· ·Â ··ÿ·» #{req.Id}.", Link = "/Profile/Index", CreatedAt = DateTime.Now });
            }

            dispute.AdminNote = adminNote;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["Success"] = " „ «·›’· ›Ì «·‰“«⁄ «·„«·Ì »‰Ã«Õ Ê≈—”«· «·≈‘⁄«—«  ··ÿ—›Ì‰.";
            return RedirectToAction("FinancialDisputes");
        }
    }
}
