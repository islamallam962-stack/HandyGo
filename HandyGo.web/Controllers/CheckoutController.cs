using HandyGo.web.Data;
using HandyGo.web.Models;
using HandyGo.web.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HandyGo.web.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public CheckoutController(AppDbContext context, IHubContext<NotificationHub> hubContext, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _hubContext = hubContext;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }



        private decimal GetTechnicianCommissionRate(User technician)
        {
            if (technician == null || technician.SubscriptionExpiry == null || technician.SubscriptionExpiry < DateTime.Now)
                return 0.10m; 

            return technician.SubscriptionPlan switch
            {
                "Tech200" => 0.08m, 
                "Tech500" => 0.05m, 
                "Tech5000" => 0.00m, 
                _ => 0.10m
            };
        }

        private decimal GetClientDiscountRate(User client)
        {
            if (client == null || client.SubscriptionExpiry == null || client.SubscriptionExpiry < DateTime.Now)
                return 0.00m; 

            return client.SubscriptionPlan switch
            {
                "Client500" => 0.05m,   
                "Client5000" => 0.10m,  
                "Client10000" => 0.15m, 
                _ => 0.00m
            };
        }



        [HttpGet]
        public async Task<IActionResult> Index(int requestId, int technicianId, decimal price, int? bidId = null)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null || HttpContext.Session.GetString("UserRole") != "Client")
                return RedirectToAction("Login", "Account");

            var client = await _context.Users.FindAsync(clientId.Value);
            var technician = await _context.Users.FindAsync(technicianId);
            var request = await _context.Requests.FindAsync(requestId);

            if (client == null || technician == null || request == null || request.ClientId != clientId)
                return NotFound("Invalid Request or Unauthorized Access");

            decimal techRate = GetTechnicianCommissionRate(technician);
            decimal clientDiscountRate = GetClientDiscountRate(client);

            decimal subDiscount = price * clientDiscountRate; 
            decimal finalPrice = price - subDiscount; 

            ViewBag.Technician = technician;
            ViewBag.Request = request;
            ViewBag.ClientWalletBalance = client.WalletBalance;
            ViewBag.OriginalPrice = price;
            ViewBag.SubDiscount = subDiscount;
            ViewBag.FinalPrice = finalPrice;
            ViewBag.BidId = bidId;

            return View();
        }



        [HttpPost]
        public async Task<IActionResult> ApplyPromoCode(string code, decimal price)
        {
            var promo = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Code == code.ToUpper().Trim() && p.IsActive);

            if (promo == null || promo.ExpiryDate < DateTime.Now || promo.CurrentUsageCount >= promo.MaxUsageCount)
            {
                return Json(new { success = false, message = "ﬂÊœ «·Œ’„ €Ì— ’«·Õ √Ê „‰ ÂÌ «·’·«ÕÌ…." });
            }

            decimal discount = 0;
            if (promo.DiscountType == "Percentage")
                discount = price * (promo.DiscountValue / 100m);
            else
                discount = promo.DiscountValue;

            if (discount > price) discount = price; 

            decimal newTotal = price - discount;
            return Json(new { success = true, discount = discount, newTotal = newTotal, message = " „  ÿ»Ìﬁ «·Œ’„ »‰Ã«Õ!" });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int requestId, int technicianId, decimal price, string paymentMethod, string promoCode = null, int? bidId = null)
        {
            var clientId = HttpContext.Session.GetInt32("UserId");
            if (clientId == null) return RedirectToAction("Login", "Account");

            var client = await _context.Users.FindAsync(clientId.Value);
            var technician = await _context.Users.FindAsync(technicianId); 
            var request = await _context.Requests.FindAsync(requestId);

            if (request == null || request.ClientId != clientId || technician == null) return NotFound();

            decimal techRate = GetTechnicianCommissionRate(technician);
            decimal clientDiscountRate = GetClientDiscountRate(client);

            decimal subDiscount = price * clientDiscountRate; 
            decimal promoDiscount = 0; 
            PromoCode appliedPromo = null;

            if (!string.IsNullOrEmpty(promoCode))
            {
                appliedPromo = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Code == promoCode.ToUpper().Trim() && p.IsActive);
                if (appliedPromo != null && appliedPromo.ExpiryDate >= DateTime.Now && appliedPromo.CurrentUsageCount < appliedPromo.MaxUsageCount)
                {
                    if (appliedPromo.DiscountType == "Percentage") promoDiscount = price * (appliedPromo.DiscountValue / 100m);
                    else promoDiscount = appliedPromo.DiscountValue;

                    appliedPromo.CurrentUsageCount++; 
                }
            }

            decimal totalDiscount = subDiscount + promoDiscount;
            if (totalDiscount > price) totalDiscount = price;

            decimal finalPriceToPay = price - totalDiscount; 
            decimal netToTech = price * (1 - techRate); 
            decimal commission = (price * techRate) - totalDiscount; 

            if (paymentMethod == "Wallet")
            {
                if (client.WalletBalance < finalPriceToPay)
                {
                    TempData["Error"] = "—’Ìœ „Õ›Ÿ ﬂ €Ì— ﬂ«›Ú. Ì—ÃÏ «Œ Ì«— ÿ—Ìﬁ… œ›⁄ √Œ—Ï.";
                    return RedirectToAction("Index", new { requestId, technicianId, price, bidId });
                }

                client.WalletBalance -= finalPriceToPay;
                request.PaymentStatus = "InEscrow";
                TempData["Success"] = " „ Œ’„ «·„»·€ „‰ „Õ›Ÿ ﬂ ÊÂÊ „⁄·ﬁ »√„«‰ Õ Ï Ì „ ≈‰Ã«“ «·⁄„·.";
            }

            else if (paymentMethod == "CreditCard")
            {
                request.PaymentStatus = "PendingCard";
                request.PaymentMethod = paymentMethod;
                request.Price = price;
                request.PlatformCommission = commission;
                request.NetToTechnician = netToTech;

                string orderReference = $"REQ_{request.Id}_{DateTime.Now.Ticks}";

                await _context.SaveChangesAsync();

                try
                {
                    string iframeUrl = await GeneratePaymobIframeUrl(finalPriceToPay, client, orderReference);
                    return Redirect(iframeUrl);
                }
                catch (Exception)
                {
                    TempData["Error"] = "ÕœÀ Œÿ√ √À‰«¡ «·« ’«· »»Ê«»… «·œ›⁄. Ì—ÃÏ «·„Õ«Ê·… ·«Õﬁ«.";
                    return RedirectToAction("Index", new { requestId, technicianId, price, bidId });
                }
            }

            else if (paymentMethod == "Cash")
            {
                request.PaymentStatus = "PendingCash";
                TempData["Success"] = $" „  √ﬂÌœ «·ÿ·» ﬂ«‘. Ì—ÃÏ  Õ÷Ì— „»·€ ({finalPriceToPay} Ã.„) ··›‰Ì.";
            }
            else
            {
                TempData["Error"] = "ÿ—Ìﬁ… œ›⁄ €Ì— ’«·Õ….";
                return RedirectToAction("Index", new { requestId, technicianId, price, bidId });
            }

            request.TechnicianId = technicianId;
            request.Status = "Accepted";
            request.Price = price;
            request.PaymentMethod = paymentMethod;
            request.PlatformCommission = commission;
            request.NetToTechnician = netToTech;

            _context.Notifications.Add(new Notification
            {
                UserId = technicianId,
                Message = $"?? ÿ·» ÃœÌœ »ﬁÌ„… {price} Ã. «·œ›⁄: {(paymentMethod == "Cash" ? "ﬂ«‘" : "«·ﬂ —Ê‰Ì („⁄·ﬁ)")}",
                Link = "/TechnicianRequests/Index",
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveNotification");

            TempData["ActiveTab"] = "requestsTab";
            return RedirectToAction("Index", "Profile");
        }



        private async Task<string> GeneratePaymobIframeUrl(decimal amountEgp, User client, string orderReference)
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



        [HttpGet]
        public async Task<IActionResult> PaymobCallback([FromQuery] string success, [FromQuery] string merchant_order_id, [FromQuery] string txn_response_code)
        {
            bool isSuccess = (success != null && success.ToLower() == "true");
            var parts = merchant_order_id?.Split('_');
            int requestId = 0;

            if (parts != null && parts.Length >= 2) int.TryParse(parts[1], out requestId);

            if (requestId == 0) return RedirectToAction("Index", "Profile");

            var request = await _context.Requests.FindAsync(requestId);
            if (request == null) return RedirectToAction("Index", "Profile");

            if (isSuccess)
            {
                request.PaymentStatus = "InEscrow";
                request.UpdatedAt = DateTime.Now;

                _context.Notifications.Add(new Notification
                {
                    UserId = request.TechnicianId ?? 0,
                    Message = $"?? «·⁄„Ì· ﬁ«„ »«·œ›⁄ «·«·ﬂ —Ê‰Ì. «·„»·€ «·¬‰ „⁄·ﬁ »√„«‰ ›Ì «·„‰’…° «»œ√ «·⁄„· ›Ê—«!",
                    Link = "/TechnicianRequests/Index",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveNotification");

                TempData["Success"] = " „ «·œ›⁄ «·≈·ﬂ —Ê‰Ì »‰Ã«Õ! «·„»·€ «·¬‰ „⁄·ﬁ »√„«‰ ›Ì «·„‰’….";
            }
            else
            {
                TempData["Error"] = $"›‘·  ⁄„·Ì… «·œ›⁄ (ﬂÊœ «·Œÿ√: {txn_response_code}). Ì—ÃÏ «·„Õ«Ê·… „—… √Œ—Ï.";
            }
            TempData["ActiveTab"] = "requestsTab";
            return RedirectToAction("Index", "Profile");
        }
    }
}
