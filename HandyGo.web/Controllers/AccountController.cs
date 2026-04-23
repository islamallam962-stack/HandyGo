using HandyGo.web.Data;
using HandyGo.web.Models;
using HandyGo.web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;
using System.Net.Mail;
using System.Net;

namespace HandyGo.web.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AccountController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }



        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return HttpContext.Session.GetString("UserRole") == "Admin"
                    ? RedirectToAction("Users", "Admin")
                    : RedirectToAction("Index", "Profile");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model, string? ReferralCodeInput = null)
        {
            ModelState.Remove("ReferralCodeInput");

            if (!ModelState.IsValid) return View(model);

            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email address is already in use.");
                return View(model);
            }

            int? referrerId = null;
            if (!string.IsNullOrEmpty(ReferralCodeInput))
            {
                var referrer = _context.Users.FirstOrDefault(u => u.ReferralCode == ReferralCodeInput.ToUpper().Trim());
                if (referrer != null)
                {
                    referrerId = referrer.Id;
                    ProcessReferralReward(referrer); 
                }
                else
                {
                    TempData["Info"] = "Note: The referral code was invalid, but your account was created.";
                }
            }

            string newReferralCode = GenerateUniqueReferralCode(model.Name);

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                Role = model.Role ?? "Client",
                Phone = model.Phone,
                ReferralCode = newReferralCode,
                ReferredByUserId = referrerId,
                WalletBalance = 0,
                IsActive = true,
                CreatedAt = DateTime.Now,
                LastSeen = DateTime.Now
            };

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                SetUserSession(user);
                TempData["Success"] = "Welcome to HandyGo! Your account has been created.";
                return RedirectToAction("Index", "Profile");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "An error occurred while saving your data.");
                return View(model);
            }
        }



        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return HttpContext.Session.GetString("UserRole") == "Admin"
                    ? RedirectToAction("Users", "Admin")
                    : RedirectToAction("Index", "Profile");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var hashed = HashPassword(model.Password);
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email && u.PasswordHash == hashed);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Your account is suspended.");
                return View(model);
            }

            SetUserSession(user);
            TempData["Success"] = $"Welcome back, {user.Name}!";
            return user.Role == "Admin" ? RedirectToAction("Users", "Admin") : RedirectToAction("Index", "Profile");
        }



        [HttpGet]
        public IActionResult ExternalLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("ExternalLoginCallback") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Google authentication failed.";
                return RedirectToAction("Login");
            }

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Unable to retrieve email from Google.";
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Name = name ?? "User",
                    Email = email,
                    Role = "Client",
                    AuthProvider = "Google",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    LastSeen = DateTime.Now,
                    WalletBalance = 0,
                    ReferralCode = GenerateUniqueReferralCode(name ?? "User")
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (!user.IsActive)
                {
                    TempData["Error"] = "This account is suspended.";
                    return RedirectToAction("Login");
                }

                if (string.IsNullOrEmpty(user.ReferralCode))
                {
                    user.ReferralCode = GenerateUniqueReferralCode(user.Name);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            SetUserSession(user);
            HttpContext.Session.SetString("UserReferralCode", user.ReferralCode ?? "");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = $"Logged in via Google as {user.Name}";

            return user.Role == "Admin" ? RedirectToAction("Users", "Admin") : RedirectToAction("Index", "Profile");
        }



        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Please enter your email address.");
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null && user.AuthProvider != "Google")
            {
                user.ResetPasswordToken = Guid.NewGuid().ToString();
                user.ResetPasswordExpiry = DateTime.Now.AddHours(1);
                _context.SaveChanges();

                var resetLink = Url.Action("ResetPassword", "Account", new { token = user.ResetPasswordToken, email = user.Email }, Request.Scheme);
                string subject = "Reset Your Password - HandyGo";
                string body = $"<h3>Hello {user.Name},</h3><p>Click the link below to reset your password:</p><a href='{resetLink}'>Reset Password</a><p>This link will expire in 1 hour.</p>";

                try { await SendEmailAsync(user.Email, subject, body); } catch { }
            }

            TempData["Success"] = "If an account exists, you will receive reset instructions.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email)) return RedirectToAction("Login");
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email && u.ResetPasswordToken == model.Token);

            if (user == null || user.ResetPasswordExpiry < DateTime.Now)
            {
                ModelState.AddModelError("", "The reset link is invalid or has expired.");
                return View(model);
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            user.ResetPasswordToken = null;
            user.ResetPasswordExpiry = null;
            _context.SaveChanges();

            TempData["Success"] = "Password updated! You can now log in.";
            return RedirectToAction("Login");
        }



        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private void SetUserSession(User user)
        {
            user.LastSeen = DateTime.Now;
            _context.Users.Update(user);
            _context.SaveChanges();

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role);

            if (!string.IsNullOrEmpty(user.ReferralCode))
            {
                HttpContext.Session.SetString("UserReferralCode", user.ReferralCode);
            }
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private string GenerateUniqueReferralCode(string name)
        {
            string cleanName = string.IsNullOrEmpty(name) ? "USER" : name.Replace(" ", "").ToUpper();
            string prefix = cleanName.Length >= 4 ? cleanName.Substring(0, 4) : cleanName.PadRight(4, 'X');
            return prefix + new Random().Next(1000, 9999).ToString();
        }

        private void ProcessReferralReward(User referrer)
        {
            int currentReferralCount = _context.Users.Count(u => u.ReferredByUserId == referrer.Id);

            if ((currentReferralCount + 1) % 5 == 0)
            {
                referrer.WalletBalance += 500;
                _context.Notifications.Add(new Notification
                {
                    UserId = referrer.Id,
                    Message = "?? Amazing! You've completed 5 referrals and earned 500 EGP.",
                    CreatedAt = DateTime.Now,
                    Link = "/Wallet"
                });
            }
            else
            {
                int remaining = 5 - ((currentReferralCount + 1) % 5);
                _context.Notifications.Add(new Notification
                {
                    UserId = referrer.Id,
                    Message = $"?? Someone joined using your code. Invite {remaining} more for the reward!",
                    CreatedAt = DateTime.Now,
                    Link = "#" 
                });
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var client = new SmtpClient(_config["EmailSettings:MailServer"], int.Parse(_config["EmailSettings:MailPort"]))
            {
                Credentials = new NetworkCredential(_config["EmailSettings:SenderEmail"], _config["EmailSettings:Password"]),
                EnableSsl = true
            };
            var mail = new MailMessage { From = new MailAddress(_config["EmailSettings:SenderEmail"], _config["EmailSettings:SenderName"]), Subject = subject, Body = body, IsBodyHtml = true };
            mail.To.Add(toEmail);
            await client.SendMailAsync(mail);
        }
    }
}
