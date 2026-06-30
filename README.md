# ShiftPlatform — Multi-Tenant Shift Management

A single deployable ASP.NET Core MVC application that serves both the **parent platform** (company registration, billing, platform owner dashboard) and **tenant workspaces** (shift management per company).

## Technology Stack

- ASP.NET Core 8 MVC (JSON API controllers for all shift data)
- SQL Server + Entity Framework Core (code-first migrations)
- ASP.NET Core Identity with role-based authorization
- Stripe Payment Intents (official Stripe.NET SDK)
- Razor views + Bootstrap 5

## Tenant Routing

**Path routing** is used (subdomain wildcard DNS is out of scope):

```
https://yourapp.com/t/{company-slug}
```

Examples:

- Workspace dashboard: `/t/acme-corp`
- Shift management: `/t/acme-corp/shifts`
- User management: `/t/acme-corp/users`

The tenant slug is generated from the company name at registration. API endpoints live at `/api/*` and resolve the tenant from the authenticated user's `TenantId` claim (set at login). Workspace pages resolve the tenant from the `/t/{slug}` path segment.

## Five-User Cap Interpretation

The **administrator counts as one of the five included users**. At registration you may add up to **four additional users** (five total). Adding a sixth user requires purchasing Pro seats at $5.00 AUD per seat.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server 2019+ (local, Docker, or Azure)
- [Stripe account](https://stripe.com) (test mode is fine)

## Quick Start

### 1. Start SQL Server (Docker)

```bash
docker compose up -d
```

This starts SQL Server on port `1433` with SA password `YourStrong!Passw0rd` (matches the default connection string).

### 2. Configure Stripe

Update `ShiftPlatform/appsettings.json` or use user secrets:

```bash
cd ShiftPlatform
dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
```

Never commit real Stripe keys.

### 3. Run Migrations & Start

Migrations run automatically on startup via `DbInitializer`. To apply manually:

```bash
cd ShiftPlatform
dotnet ef database update
dotnet run
```

Open `https://localhost:5001` (or the URL shown in the console).

### 4. Stripe Webhook (local development)

Use the [Stripe CLI](https://stripe.com/docs/stripe-cli):

```bash
stripe listen --forward-to https://localhost:5001/api/stripe/webhook
```

Copy the webhook signing secret into `Stripe:WebhookSecret`. Webhooks confirm payment status for both registration validation ($1.00 AUD) and seat purchases ($5.00 AUD per seat).

## Seed Data

On first run, the database is seeded with:

| Role | Email | Password |
|------|-------|----------|
| Platform Owner | `owner@platform.local` | `OwnerPass123!` |
| Company Admin (Acme Corp) | `admin@acme.local` | `AdminPass123!` |
| User | `alice@acme.local` | `UserPass123!` |
| User | `bob@acme.local` | `UserPass123!` |

Sample tenant workspace: `/t/acme-corp`

## Connection String

Default in `appsettings.json`:

```
Server=localhost,1433;Database=ShiftPlatform;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true
```

## Architecture

### Tenant Isolation

- Every tenant-owned entity has a `TenantId` foreign key.
- EF Core **global query filters** enforce `TenantId == current tenant` on `Shift`, `ShiftAssignment`, `SwapRequest`, and `AccessLog`.
- Filters apply whenever `ITenantContext` has a tenant set (from route or authenticated user).

### Roles

| Role | Access |
|------|--------|
| **PlatformOwner** | Parent platform dashboard — registrations, workspaces, payments, seat purchases, access logs. Cannot see tenant shift data. |
| **CompanyAdmin** | Full workspace — users, shifts, assignments, swap approval, seat purchases |
| **User** | Own shifts, swap requests, dashboard |

### Parent Platform (`Controllers/Platform/`)

- Company registration with $1.00 AUD Stripe validation
- Login with tenant-aware routing
- Platform owner tracking dashboard (paginated registrations)

### Tenant Workspace (`Controllers/Tenant/` + `Controllers/Api/`)

All shift CRUD goes through API endpoints; Razor views call them via JavaScript.

| Endpoint | Description |
|----------|-------------|
| `GET /api/shifts` | List shifts (filter by date range, user) |
| `POST /api/shifts` | Create shift (admin) |
| `PUT /api/shifts/{id}` | Update shift (admin) |
| `DELETE /api/shifts/{id}` | Delete shift (admin) |
| `POST /api/shifts/{id}/assign` | Assign shift to user (admin) |
| `GET /api/users` | List tenant users |
| `POST /api/swaps` | Raise swap request |
| `GET /api/swaps` | List swap requests |
| `POST /api/swaps/{id}/approve` | Approve swap (target user or admin) |
| `POST /api/swaps/{id}/reject` | Reject swap |
| `GET /api/dashboard` | Assigned + upcoming shifts (4-week window) |

### Payments

**Registration validation:** $1.00 AUD (100 cents) via Payment Intent. Nothing is created until payment succeeds. Failed/abandoned payments leave only a `RegistrationSubmission` record — no tenant, users, or workspace.

**Seat upgrades:** $5.00 AUD (500 cents) per seat, one-off charge (not a subscription). On success, `SeatAllowance` increases and tier moves to Pro. Server-side enforcement blocks adding users beyond paid seats.

#### Switching to authorization-only or refunds

The validation Payment Intent uses `capture_method: automatic` (charge is captured). To authorize without capturing, set `CaptureMethod = "manual"` in `StripePaymentService.CreateValidationPaymentIntentAsync` and capture or cancel after validation. To refund immediately after validation, call `RefundService.CreateAsync` with the Payment Intent ID after provisioning.

#### Switching seat purchases to subscriptions

Replace `CreateSeatPaymentIntentAsync` with Stripe Subscription creation (e.g. per-seat price of $5 AUD/month). Store `SubscriptionId` on the tenant and listen for `invoice.paid` / `customer.subscription.deleted` webhooks to adjust `SeatAllowance`.

## Project Structure

```
ShiftPlatform/
├── Controllers/
│   ├── Platform/     # Registration, login, platform dashboard
│   ├── Tenant/       # Workspace MVC views
│   └── Api/          # Shift, user, swap, dashboard, Stripe webhook
├── Data/             # DbContext, migrations, seeding
├── Models/           # Entities, enums, view models
├── Services/         # Stripe, registration, provisioning, seats
├── Middleware/       # Tenant resolution
└── Views/            # Razor + Bootstrap 5
```

## Development

```bash
dotnet build
dotnet test   # (add tests as needed)
dotnet ef migrations add MigrationName
dotnet ef database update
```

## License

MIT
