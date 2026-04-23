<div align="center">

# 🔧 HandyGo

**A full-stack platform connecting clients with verified technicians — built with ASP.NET Core MVC**

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon-336791?style=flat-square&logo=postgresql)](https://neon.tech/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--time-FF6B00?style=flat-square)](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
[![Paymob](https://img.shields.io/badge/Paymob-Payment%20Gateway-00A859?style=flat-square)](https://paymob.com/)

</div>

---

## 📖 About

HandyGo is a service marketplace platform built for the Egyptian market. It allows clients to post service requests and receive competitive bids from verified technicians across multiple categories (plumbing, electrical, carpentry, etc.). The platform handles everything from booking to secure payment, real-time communication, and financial management — all in one place.

> ⚠️ **Note:** This repository is for portfolio display only. The `appsettings.json` contains placeholder values. To run locally, you must supply your own credentials (see [Configuration](#️-configuration)).

---

## ✨ Features

### 👤 Authentication & Users
- Email & password registration with SHA-256 hashing
- Google OAuth 2.0 login
- Forgot password with email reset link (SMTP / Gmail)
- Role-based access: **Client**, **Technician**, **Admin**
- Session-based authentication

### 🛠️ Service Requests & Bidding
- Clients post service requests with type, address, and description
- Technicians browse open jobs and submit price bids
- Clients review bids and select their technician
- Live map view of nearby technicians

### 💳 Payments & Wallet
- **Wallet** system — clients recharge, technicians withdraw
- **Escrow** — funds held until job is confirmed complete
- **Credit card** payments via Paymob payment gateway
- **Cash** payment option
- Promo code system (percentage & fixed discounts)
- Subscription plans affecting commission rates and discounts
- Financial dispute resolution by admin

### 💬 Real-time Chat
- In-app messaging between client and technician per request
- Image sharing in chat
- Unread message badge counter
- Chat inbox with last message preview
- Powered by **SignalR WebSockets**

### 🔔 Notifications
- Real-time push notifications via SignalR
- Notification types: new bids, messages, payment updates, admin warnings, withdrawals
- Mark as read / clear all

### ⭐ Reviews & Ratings
- Clients rate technicians after job completion (1–5 stars)
- Reviews visible on technician profiles
- Admin can monitor low-rated technicians

### 🏆 Referral & Affiliate System
- Each user gets a unique referral code on signup
- Every 5 successful referrals = **500 EGP** reward
- Affiliate dashboard for admin to track top marketers

### 🛡️ Admin Panel
- Full user management (ban, activate, delete, warn)
- Service request statistics (today / week / month / year)
- Financial overview (escrow, earnings, pending withdrawals)
- Technician verification requests (approve / reject)
- Promo code management (create, toggle active)
- Financial dispute resolution (refund client or release to technician)
- Global announcement notifications to all users
- Complaint management system

### 📱 PWA Support
- Installable as a Progressive Web App
- Service Worker with offline caching

---

## 🏗️ Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 9 MVC |
| Language | C# |
| Database | PostgreSQL (hosted on Neon) |
| ORM | Entity Framework Core |
| Real-time | SignalR |
| Payment | Paymob API |
| Auth | Cookie Auth + Google OAuth 2.0 |
| Email | SMTP (Gmail App Password) |
| Frontend | Razor Views, Bootstrap 5, Vanilla JS |
| PWA | Service Worker + Web App Manifest |

---

## 🗂️ Project Structure

```
HandyGo/
├── Controllers/
│   ├── AccountController.cs      # Auth, Google login, password reset
│   ├── AdminController.cs        # Admin panel, financials, verifications
│   ├── ChatController.cs         # Real-time messaging
│   ├── CheckoutController.cs     # Payment processing, Paymob integration
│   ├── OffersController.cs       # Browse technicians, live map
│   ├── OffersPriceController.cs  # Bid management
│   ├── ProfileController.cs      # User profiles, upgrade to technician
│   ├── RequestServicesController.cs
│   ├── SubscriptionController.cs
│   ├── TechnicianRequestsController.cs
│   └── WalletController.cs
├── Models/                       # EF Core entities
├── ViewModels/                   # Form/display models
├── Hubs/
│   ├── ChatHub.cs               # SignalR chat
│   └── NotificationHub.cs       # SignalR notifications
├── Data/
│   └── AppDbContext.cs
├── Migrations/
├── Views/
└── wwwroot/                     # Static assets, PWA files
```

---

## ⚙️ Configuration

Create your own `appsettings.json` (not committed) using this template:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_POSTGRESQL_CONNECTION_STRING"
  },
  "Paymob": {
    "ApiKey": "YOUR_PAYMOB_API_KEY",
    "IntegrationId": "YOUR_INTEGRATION_ID",
    "IframeId": "YOUR_IFRAME_ID"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "EmailSettings": {
    "MailServer": "smtp.gmail.com",
    "MailPort": 587,
    "SenderName": "HandyGo Support",
    "SenderEmail": "YOUR_GMAIL",
    "Password": "YOUR_GMAIL_APP_PASSWORD"
  }
}
```

---

## 🚀 Running Locally

```bash
# 1. Clone the repository
git clone https://github.com/islamallam962-stack/HandyGo.git
cd HandyGo

# 2. Add your appsettings.json with real credentials
#2.1
dotnet restore
#2.2
dotnet build
# 3. Apply migrations
dotnet ef database update

# 4. Run
dotnet run --project HandyGo.web
```

---

## 📸 Screenshots

> Coming soon

---

## 👨‍💻 Author

**Islam Allam** [LinkedIn Profile](https://www.linkedin.com/in/islam-allam-1a4bb73a9/)
---

---

<div dir="rtl">

---

## 📖 نبذة عن المشروع

HandyGo منصة خدمات مبنية للسوق المصري، بتخلي العملاء يطلبوا خدمات صيانة وفنيين متخصصين بكل سهولة، والفنيين يقدروا يقدموا عروض أسعار تنافسية. المنصة بتتكفل بكل حاجة من الحجز لحد الدفع الآمن والتواصل اللحظي وإدارة المدفوعات.

> ⚠️ **ملاحظة:** الريبو ده لعرض الشغل فقط. ملف `appsettings.json` فيه قيم placeholder — عشان تشغّل المشروع محلياً محتاج تحط credentials حقيقية.

---

## ✨ المميزات

### 👤 المصادقة والمستخدمين
- تسجيل بالإيميل وكلمة سر مع تشفير SHA-256
- تسجيل دخول بـ Google OAuth 2.0
- نسيت كلمة السر مع رابط reset على الإيميل
- أدوار مستخدمين: **عميل** / **فني** / **أدمن**
- مصادقة بالـ Session

### 🛠️ الطلبات والعروض
- العملاء ينشروا طلبات خدمة بالنوع والعنوان والوصف
- الفنيين يشوفوا الوظايف المتاحة ويقدموا عروض أسعار
- العملاء يراجعوا العروض ويختاروا الفني
- خريطة حية للفنيين القريبين

### 💳 الدفع والمحفظة
- نظام **محفظة** — العملاء يشحنوا، الفنيين يسحبوا
- نظام **Escrow** — الفلوس محجوزة لحد تأكيد إتمام الشغل
- دفع ببطاقة الائتمان عن طريق **Paymob**
- دفع كاش
- نظام بروموكود (خصم نسبة أو قيمة ثابتة)
- باقات اشتراك بتأثر على نسب العمولة والخصومات
- فض النزاعات المالية عن طريق الأدمن

### 💬 الشات اللحظي
- محادثات بين العميل والفني لكل طلب
- إرسال صور في الشات
- عداد الرسائل غير المقروءة
- صندوق الوارد مع آخر رسالة
- مبني على **SignalR WebSockets**

### 🔔 الإشعارات
- إشعارات فورية عن طريق SignalR
- أنواع الإشعارات: عروض جديدة، رسايل، تحديثات دفع، تحذيرات من الأدمن، طلبات سحب
- تأشير كـ مقروء / مسح الكل

### ⭐ التقييمات
- العملاء يقيّموا الفنيين بعد الانتهاء (1-5 نجوم)
- التقييمات تظهر على بروفايل الفني
- الأدمن يراقب التقييمات المنخفضة

### 🏆 نظام الإحالة والأفيليات
- كل مستخدم بياخد كود إحالة فريد وقت التسجيل
- كل 5 إحالات ناجحة = **500 جنيه** مكافأة
- داشبورد للأدمن لتتبع أكبر المسوقين

### 🛡️ لوحة الأدمن
- إدارة كاملة للمستخدمين (حظر، تفعيل، حذف، تحذير)
- إحصائيات الطلبات (اليوم / الأسبوع / الشهر / السنة)
- نظرة مالية شاملة (escrow، الأرباح، السحوبات المعلقة)
- طلبات التحقق للفنيين (موافقة / رفض)
- إدارة البروموكودات
- فض النزاعات المالية
- إرسال إشعارات عامة لجميع المستخدمين
- إدارة الشكاوى

### 📱 PWA
- قابل للتثبيت كـ Progressive Web App
- Service Worker مع offline caching

---

## 🏗️ التقنيات المستخدمة

| الطبقة | التقنية |
|---|---|
| الفريموورك | ASP.NET Core 9 MVC |
| اللغة | C# |
| قاعدة البيانات | PostgreSQL (Neon) |
| ORM | Entity Framework Core |
| Real-time | SignalR |
| الدفع | Paymob API |
| المصادقة | Cookie Auth + Google OAuth 2.0 |
| الإيميل | SMTP (Gmail) |
| الفرونت | Razor Views, Bootstrap 5, Vanilla JS |
| PWA | Service Worker + Web App Manifest |

---

## 👨‍💻 المطور

---
### 👨‍💻 Developed By:
**Islam Allam** [LinkedIn Profile](https://www.linkedin.com/in/islam-allam-1a4bb73a9/)
</div>
