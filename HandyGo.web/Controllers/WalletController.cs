using HandyGo.web.Data;
using HandyGo.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class WalletController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public WalletController(AppDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId.Value);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> RequestWithdrawal(decimal amount, string withdrawMethod, string details)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || HttpContext.Session.GetString("UserRole") != "Technician")
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId.Value);

            if (amount < 50 || amount > user.WalletBalance)
            {
                TempData["Error"] = "The requested amount exceeds your available balance (Minimum withdrawal is 50 EGP).";
                return RedirectToAction("Index");
            }

            user.WalletBalance -= amount;

            var withdrawalRequest = new WithdrawalRequest
            {
                TechnicianId = user.Id,
                Amount = amount,
                Method = withdrawMethod,
                Details = details,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };
            _context.WithdrawalRequests.Add(withdrawalRequest);

            var adminNotification = new Notification
            {
                UserId = 1, 
                Message = $"?? New withdrawal request: {user.Name} is requesting to withdraw {amount} EGP via {withdrawMethod}.",
                Link = "/Admin/Financials", 
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(adminNotification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your request is being processed. The amount will be transferred within a maximum of 24 hours.";

            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> Recharge(decimal amount)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a valid amount to recharge your wallet.";
                return RedirectToAction("Index");
            }

            var user = await _context.Users.FindAsync(userId.Value);

            string orderReference = $"WALLET_{user.Id}_{DateTime.Now.Ticks}";

            try
            {

                string iframeUrl = await GeneratePaymobRechargeUrl(amount, user, orderReference);

                return Redirect(iframeUrl);
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while connecting to the payment gateway. Please try again later.";
                return RedirectToAction("Index");
            }
        }



        [HttpGet]
        public async Task<IActionResult> PaymobRechargeCallback([FromQuery] string success, [FromQuery] string merchant_order_id, [FromQuery] string txn_response_code, [FromQuery] string amount_cents)
        {
            bool isSuccess = (success != null && success.ToLower() == "true");


            var parts = merchant_order_id?.Split('_');
            int userId = 0;

            if (parts != null && parts.Length >= 2)
            {
                int.TryParse(parts[1], out userId);
            }

            if (userId == 0)
            {
                TempData["Error"] = "Error reading client data from the payment gateway.";
                return RedirectToAction("Index");
            }

            if (isSuccess)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {

                    decimal amountEgp = 0;
                    if (!string.IsNullOrEmpty(amount_cents) && decimal.TryParse(amount_cents, out decimal cents))
                    {
                        amountEgp = cents / 100m;
                    }

                    user.WalletBalance += amountEgp;
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Your wallet has been successfully recharged with {amountEgp} EGP via credit card!";
                }
            }
            else
            {
                TempData["Error"] = $"Recharge failed (Code: {txn_response_code}). Please try again.";
            }

            return RedirectToAction("Index");
        }



        private async Task<string> GeneratePaymobRechargeUrl(decimal amountEgp, User client, string orderReference)
        {
            var clientHttp = _httpClientFactory.CreateClient();
            string apiKey = _config["Paymob:ApiKey"];
            string integrationId = _config["Paymob:IntegrationId"];
            string iframeId = _config["Paymob:IframeId"];

            int amountInCents = (int)(amountEgp * 100);

            var authPayload = new { api_key = apiKey };
            var authResponse = await clientHttp.PostAsync("https://accept.paymob.com/api/auth/tokens",
                new StringContent(JsonSerializer.Serialize(authPayload), Encoding.UTF8, "application/json"));

            var authResult = JsonSerializer.Deserialize<JsonElement>(await authResponse.Content.ReadAsStringAsync());
            string authToken = authResult.GetProperty("token").GetString();

            var orderPayload = new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = amountInCents.ToString(),
                currency = "EGP",
                merchant_order_id = orderReference
            };
            var orderResponse = await clientHttp.PostAsync("https://accept.paymob.com/api/ecommerce/orders",
                new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json"));

            var orderResult = JsonSerializer.Deserialize<JsonElement>(await orderResponse.Content.ReadAsStringAsync());
            string orderId = orderResult.GetProperty("id").GetInt32().ToString();

            var paymentKeyPayload = new
            {
                auth_token = authToken,
                amount_cents = amountInCents.ToString(),
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    apartment = "NA",
                    email = client.Email,
                    floor = "NA",
                    first_name = client.Name,
                    street = "NA",
                    building = "NA",
                    phone_number = client.Phone ?? "01000000000",
                    shipping_method = "NA",
                    postal_code = "NA",
                    city = "Cairo",
                    country = "EG",
                    last_name = "Client",
                    state = "NA"
                },
                currency = "EGP",
                integration_id = integrationId
            };
            var keyResponse = await clientHttp.PostAsync("https://accept.paymob.com/api/acceptance/payment_keys",
                new StringContent(JsonSerializer.Serialize(paymentKeyPayload), Encoding.UTF8, "application/json"));

            var keyResult = JsonSerializer.Deserialize<JsonElement>(await keyResponse.Content.ReadAsStringAsync());
            string paymentToken = keyResult.GetProperty("token").GetString();

            return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";
        }
    }
}
