# ShiftManager — Multi-Tenant Shift Management Platform

A multi-tenant web platform built with ASP.NET Core MVC, SQL Server, Entity Framework Core, ASP.NET Core Identity, and Stripe.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Technology Stack](#technology-stack)
3. [Tenant Routing Approach](#tenant-routing-approach)
4. [Five-User Cap Interpretation](#five-user-cap-interpretation)
5. [Seat Purchase Model](#seat-purchase-model)
6. [Setup & Prerequisites](#setup--prerequisites)
7. [Configuration](#configuration)
8. [Running Migrations](#running-migrations)
9. [Running the Application](#running-the-application)
10. [Seeded Accounts](#seeded-accounts)
11. [Stripe Setup](#stripe-setup)
12. [Project Structure](#project-structure)
13. [API Endpoints](#api-endpoints)

---

## Architecture Overview

ShiftManager is a **single deployable application** serving both the parent platform and all tenant workspaces. There is no per-company deployment.

**"Provisioning a workspace"** means:
- Creating a `Tenant` record
- Seeding template shift data
- Creating user accounts (admin + additional users)
- Recording a `ProvisioningRecord`
- Recording a `PaymentRecord` for the validation charge

**Tenant isolation** is enforced via EF Core [global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters) on every tenant-owned entity (`Shift`, `ShiftAssignment`, `SwapRequest`, `PaymentRecord`, `SeatPurchase`, `AccessLog`, `ProvisioningRecord`). A filter of the form:

```csharp
builder.Entity<Shift>().HasQueryFilter(s =>
    s.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
```

is applied at `DbContext` configuration time, so any LINQ query that forgets a `Where(s => s.TenantId == x)` clause is still safe — the global filter ensures no cross-tenant data leaks. Queries that deliberately bypass tenant scoping (e.g., platform owner reads) call `.IgnoreQueryFilters()`.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend & API | ASP.NET Core 8 MVC — API controllers return JSON for all shift data operations |
| Database | SQL Server 2022 (SQLite for local development via `USE_SQLITE=true`) |
| ORM | Entity Framework Core 8 — code-first with migrations |
| Authentication | ASP.NET Core Identity with role-based authorisation |
| Payments | Stripe .NET SDK v46 — Payment Intents |
| Frontend | Razor views with Bootstrap 5 |

---

## Tenant Routing Approach

**Path routing** is used: `yourapp.com/t/{company-slug}/...`

Examples:
- `http://localhost:5000/t/acme-corp` — Acme Corp workspace dashboard
- `http://localhost:5000/t/acme-corp/WorkspaceHome/Shifts` — shifts page
- `http://localhost:5000/t/acme-corp/api/shifts` — shifts API

The slug is derived from the company name at registration (e.g., "Acme Corp" → `acme-corp`) and is guaranteed unique. The `TenantResolutionMiddleware` extracts the slug from the URL path and sets `ITenantContext.CurrentTenantId`, which the global query filters then use automatically.

Subdomain routing (e.g., `acme-corp.yourapp.com`) was not implemented because it requires wildcard DNS configuration that is out of scope for a self-contained deployment.

---

## Five-User Cap Interpretation

**Interpretation used:** Five users total including the admin.

- The Company Admin account counts as 1 seat.
- Up to 4 additional users can be added at registration time.
- Total free tier: 5 users (admin + 4 additional).

This is enforced server-side in `RegistrationController.Complete` (checks `AdditionalUsers.Count > 4`) and in `SeatsController.AddUser` (checks `currentUserCount >= tenant.SeatAllowance`).

---

## Seat Purchase Model

Seat purchases are **one-off charges** (not recurring subscriptions).

- Each seat costs **$5.00 AUD** per purchase via Stripe Payment Intents.
- When a seat is purchased, `Tenant.SeatAllowance` is incremented by the number of seats bought.
- When the first seat beyond the included 5 is purchased, the company is automatically moved from `Base` to `Pro` tier.
- There is no monthly billing or subscription. To switch to recurring billing, replace the Payment Intents flow with a Stripe Subscription and a price object set to `recurring: { interval: 'month' }`.

---

## Setup & Prerequisites

### Requirements
- .NET 8 SDK
- SQL Server 2022 (or enable SQLite mode for development)
- A Stripe account with test keys

### Install .NET 8
```bash
# Ubuntu 22.04+
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install dotnet-sdk-8.0
```

---

## Configuration

Edit `ShiftManager/appsettings.json` or use [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ShiftManagerDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;",
    "SqliteConnection": "Data Source=shiftmanager.db"
  },
  "Stripe": {
    "PublishableKey": "pk_test_...",
    "SecretKey": "sk_test_...",
    "WebhookSecret": "whsec_..."
  }
}
```

### Using .NET User Secrets (recommended for local development)

```bash
cd ShiftManager
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
```

### SQLite mode (for development without SQL Server)

Set the environment variable `USE_SQLITE=true` before running. The app will use `shiftmanager.db` in the project directory.

```bash
export USE_SQLITE=true
dotnet run
```

---

## Running Migrations

### Against SQL Server (production)
```bash
cd ShiftManager
dotnet ef database update
```

### Against SQLite (development)
```bash
export USE_SQLITE=true
dotnet ef database update
```

The `ApplicationDbContextFactory` used by `dotnet ef` always targets SQLite for design-time migration generation, so migration files are database-agnostic (they use column types that work on both providers).

To add a new migration:
```bash
dotnet ef migrations add <MigrationName>
```

---

## Running the Application

### With SQL Server
```bash
cd ShiftManager
dotnet run --urls=http://localhost:5000
```

### With SQLite (development)
```bash
cd ShiftManager
USE_SQLITE=true dotnet run --urls=http://localhost:5000
```

The application seeds the database on first run:
- Platform Owner account
- Sample tenant (Acme Corp) with admin, two users, and sample shifts

---

## Seeded Accounts

| Role | Email | Password | Tenant |
|---|---|---|---|
| Platform Owner | `owner@shiftmanager.com` | `Owner@123456!` | — |
| Company Admin | `admin@acme-corp.com` | `Admin@123456!` | Acme Corp |
| User | `bob@acme-corp.com` | `Bob@123456!` | Acme Corp |
| User | `carol@acme-corp.com` | `Carol@123456!` | Acme Corp |

**Workspace URL:** `/t/acme-corp`

---

## Stripe Setup

### Test Keys
1. Sign in at [dashboard.stripe.com](https://dashboard.stripe.com)
2. Go to **Developers → API keys**
3. Copy the **Publishable key** and **Secret key** (test mode)
4. Add them to your configuration

### Webhook Setup (local development)
```bash
# Install Stripe CLI
stripe listen --forward-to localhost:5000/api/stripe/webhook
```

The CLI will output a webhook signing secret (`whsec_...`). Add it to your configuration as `Stripe:WebhookSecret`.

### Webhook events handled
- `payment_intent.succeeded` — marks payment records as `Succeeded`
- `payment_intent.payment_failed` — marks payment records as `Failed`
- `payment_intent.canceled` — marks payment records as `Cancelled`

### Payment amounts
| Charge | Amount |
|---|---|
| Registration validation | 100 cents ($1.00 AUD) |
| Seat upgrade (per seat) | 500 cents ($5.00 AUD) |

### Refunding the validation charge
By default the $1.00 AUD validation charge is **captured** (the customer is charged). To switch to an authorisation-only flow (no capture), change `AutomaticPaymentMethods.Enabled = true` in `StripeService.CreatePaymentIntentAsync` to use `CaptureMethod = "manual"`. Then call `stripe.paymentIntents.capture(id)` only after provisioning, or call `stripe.paymentIntents.cancel(id)` to release the hold.

To refund after capture, call `stripe.refunds.create({ payment_intent: id })` from your webhook handler or a post-provisioning step.

---

## Project Structure

```
ShiftManager/
├── Controllers/
│   ├── Api/                     # JSON API controllers (shift data operations)
│   │   ├── ShiftsApiController.cs
│   │   ├── SwapsApiController.cs
│   │   ├── UsersApiController.cs
│   │   ├── DashboardApiController.cs
│   │   └── StripeWebhookController.cs
│   ├── Platform/                # Parent platform controllers
│   │   ├── AccountController.cs
│   │   ├── RegistrationController.cs
│   │   └── PlatformDashboardController.cs
│   ├── Workspace/               # Tenant workspace controllers
│   │   ├── WorkspaceBaseController.cs
│   │   ├── WorkspaceHomeController.cs
│   │   └── SeatsController.cs
│   └── HomeController.cs
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core context with global query filters
│   ├── SeedData.cs              # Database seeding
│   └── ApplicationDbContextFactory.cs
├── Middleware/
│   └── TenantResolutionMiddleware.cs  # Extracts tenant from /t/{slug}/...
├── Migrations/                  # EF Core migrations
├── Models/                      # Domain entities
├── Services/
│   ├── ITenantContext.cs        # Scoped tenant resolution contract
│   ├── TenantContext.cs
│   ├── StripeService.cs
│   └── TenantProvisioningService.cs
├── ViewModels/                  # Typed view models
├── Views/
│   ├── Account/                 # Login, access denied
│   ├── Home/                    # Landing page
│   ├── PlatformDashboard/       # Platform owner views
│   ├── Registration/            # Company registration
│   ├── Seats/                   # Seat upgrade views
│   └── WorkspaceHome/           # Tenant workspace views
└── wwwroot/                     # Static assets
```

---

## API Endpoints

All workspace API endpoints are scoped to a tenant via the route prefix `/t/{slug}/api/`.

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/t/{slug}/api/shifts` | Any | List shifts (admin sees all, user sees own) |
| POST | `/t/{slug}/api/shifts` | CompanyAdmin | Create a shift |
| PUT | `/t/{slug}/api/shifts/{id}` | CompanyAdmin | Update a shift |
| DELETE | `/t/{slug}/api/shifts/{id}` | CompanyAdmin | Delete a shift |
| POST | `/t/{slug}/api/shifts/{id}/assign` | CompanyAdmin | Assign shift to user |
| GET | `/t/{slug}/api/users` | Any | List tenant users |
| POST | `/t/{slug}/api/swaps` | Any | Create swap request |
| GET | `/t/{slug}/api/swaps` | Any | List swap requests |
| POST | `/t/{slug}/api/swaps/{id}/approve` | CompanyAdmin or TargetUser | Approve swap |
| POST | `/t/{slug}/api/swaps/{id}/reject` | CompanyAdmin or TargetUser | Reject swap |
| GET | `/t/{slug}/api/dashboard` | Any | Dashboard data (next 4 weeks) |
| POST | `/api/stripe/webhook` | Stripe signed | Stripe webhook handler |

### Query parameters for GET /shifts
- `from` — ISO date, filter shifts from this date
- `to` — ISO date, filter shifts up to this date
- `userId` — filter by assigned user ID (CompanyAdmin only)

---

## Upcoming Shift Window

The dashboard and `/api/dashboard` return shifts for the **next 4 weeks** (28 days from today). This window is defined in `WorkspaceHomeController` and `DashboardApiController` as `UpcomingWindowDays = 28`.
