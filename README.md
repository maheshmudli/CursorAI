# ShiftPlatform — Multi-Tenant Shift Management Platform

A single, multi-tenant ASP.NET Core MVC application with two layers:

1. **Parent platform** — companies register, pay a 1.00 AUD card-validation
   charge through Stripe, and get an isolated shift-management workspace
   provisioned from a template. The platform keeps a full record of every
   registration, provisioned workspace, payment, seat purchase, and access record.
2. **Provisioned workspace** — each tenant runs its own shift operations
   (create/assign shifts, request and approve swaps, dashboards), fully scoped
   to that tenant.

It is **one deployable** that serves both the parent platform and every tenant
workspace. Provisioning a workspace means creating a tenant row, seeding starter
data, creating the supplied user accounts, and routing the company to its own
isolated area — **not** a separate site or database per company.

## Technology stack

| Concern | Choice |
| --- | --- |
| Backend / API | ASP.NET Core 8 MVC (+ API controllers returning JSON) |
| Database | SQL Server |
| ORM | EF Core 8 (code-first, migrations) |
| Auth | ASP.NET Core Identity, role-based authorisation |
| Payments | Stripe (official `Stripe.net` SDK, Payment Intents) |
| Frontend | Razor views + Bootstrap 5 |

## Project layout

```
src/ShiftPlatform/
  Controllers/            Parent-platform controllers (Home, Register, Account, Platform)
  Controllers/Api/        Tenant-scoped JSON API (Shifts, Users, Swaps, Dashboard, Webhook)
  Controllers/WorkspaceController.cs   Tenant workspace Razor pages + seat-purchase flow
  Data/                   DbContext, migrations, seeder, design-time factory
  Middleware/             TenantResolutionMiddleware (tenant from route + access checks)
  Models/                 EF entities
  Services/               Payment gateway (Stripe + dev fake), provisioning, seats, tenant context
  ViewModels/             Form models and DTOs
  Views/                  Razor views (Bootstrap 5)
```

Parent-platform controllers and tenant-workspace controllers are kept cleanly
separated (`Controllers/*` vs `Controllers/WorkspaceController.cs` +
`Controllers/Api/*`).

## Tenant routing

**Path routing is used:** `https://yourapp.com/t/{company-slug}/...`.
Subdomain wildcard routing is out of scope (it needs DNS/host setup), so path
routing was chosen. The current tenant is resolved from the `{slug}` route value
by `TenantResolutionMiddleware`, which:

- looks up the tenant and populates a scoped `ITenantContext`;
- the `ApplicationDbContext` reads `ITenantContext.TenantId` in **EF Core global
  query filters** on `Shift`, `ShiftAssignment`, and `SwapRequest`, so tenant
  data can never leak through a forgotten `WHERE` clause;
- rejects (`403`) any authenticated user whose `TenantId` claim does not match
  the workspace they are trying to reach, and returns `404` for unknown slugs.

Platform-level queries that must span tenants (the owner dashboard) explicitly
use `IgnoreQueryFilters()`.

## Roles

- **PlatformOwner** — sees all registrations, workspaces, payments, seat
  purchases, and access records on the tracking dashboard. Cannot open a
  tenant's workspace/shift data.
- **CompanyAdmin** — the registrant. Manages users within the paid seat
  allowance, buys extra Pro seats, creates/assigns shifts, approves swaps.
- **User** — a member added by the admin. Views their own/upcoming shifts and
  raises swap requests.

## The five-user cap (interpretation)

**The admin counts as one of the five included seats.** A brand-new company may
therefore have the admin plus **up to four** additional members at registration
(five users total). The cap is enforced **server side** in `RegisterController`
(registration) and `UsersApiController` (adding users later), not just in the
browser. Adding a sixth user requires buying a seat (see below).

## Payments (Stripe)

### Validation charge (registration)

- A **1.00 AUD** (`amount = 100`, `currency = aud`) Payment Intent is created
  when the form is submitted. **Nothing in the tenant graph is created until the
  Payment Intent's status is `succeeded`.**
- Card details are collected with **Stripe Elements** on the payment page, so
  raw card data never touches the server.
- The status is **re-checked server side** before provisioning, and a **Stripe
  webhook** (`POST /webhook/stripe`) confirms the final status independently of
  the browser.
- The charge is **captured by default**. To avoid keeping the dollar you can
  either:
  - switch to an authorisation that is released — set
    `CaptureMethod = "manual"` in `StripePaymentGateway.CreatePaymentIntentAsync`
    and cancel/leave the intent uncaptured; or
  - refund it immediately after validation with `RefundService` /
    `new RefundService().CreateAsync(new RefundCreateOptions { PaymentIntent = id })`.

### Pro license & seat upgrades

- Base plan includes **5 seats**. Extra seats cost **5.00 AUD each**
  (`amount = 500` per seat, `currency = aud`), bought via the same Payment Intent
  approach. Buying several at once charges `500 × seats`.
- On a **succeeded** seat payment the tenant's `SeatAllowance` is incremented and
  the tenant is moved to the **Pro** tier. The increment is **idempotent**
  (guarded by `SeatPurchase.Applied`) so a replayed webhook can't double-grant.
- Each seat purchase records its Stripe Payment Intent id, amount, status, and
  seat count, visible to the Platform Owner.
- **Seat purchases are one-off per-seat charges** (matching the validation
  flow). To switch to recurring monthly billing later, replace the Payment
  Intent with a Stripe `Subscription` (create a Price per seat and a
  `SubscriptionService.CreateAsync`), and apply seats on
  `invoice.payment_succeeded` / `customer.subscription.updated` webhooks instead
  of `payment_intent.succeeded`.

### Stripe keys & webhook setup

Keys are read from configuration / user secrets (never hard-coded) under the
`Stripe` section:

```bash
cd src/ShiftPlatform
dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey"      "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret"  "whsec_..."
```

Webhook (local): `stripe listen --forward-to https://localhost:5001/webhook/stripe`
and subscribe to `payment_intent.succeeded` / `payment_intent.payment_failed`.

### Developer mode (no Stripe keys)

If `Stripe:SecretKey` is **not** configured, the app uses an in-memory
**`FakePaymentGateway`** so the whole flow can be exercised locally without real
card processing. The payment pages then show "Simulate successful/failed
payment" buttons. As soon as you set a real `sk_...` key, the real
`StripePaymentGateway` (Stripe Elements + Payment Intents) is used instead. This
selection happens in `Program.cs`.

## Setup & running

### Prerequisites

- .NET 8 SDK
- SQL Server (any edition; Developer/Express is fine)

### Connection string

Set in `src/ShiftPlatform/appsettings.json` (or user secrets / env var
`ConnectionStrings__DefaultConnection`). Default:

```
Server=localhost,1433;Database=ShiftPlatform;User Id=sa;Password=YourStr0ng!Passw0rd;TrustServerCertificate=True;Encrypt=False
```

### Migrations

Migrations are applied automatically on startup (`db.Database.MigrateAsync()`),
and the seed data is created. To run them manually:

```bash
cd src/ShiftPlatform
dotnet ef database update
```

To add a migration after model changes:

```bash
dotnet ef migrations add <Name> -o Data/Migrations
```

### Run

```bash
cd src/ShiftPlatform
dotnet run
```

Browse to the URL printed in the console (e.g. `http://localhost:5064`).

## Seeded data

`DbSeeder` creates (idempotently):

- **Platform Owner** — `owner@platform.test` / `Owner#12345`
- **Sample tenant** `Acme Pty Ltd` at route `/t/acme` (Base tier, 5 seats), with
  a provisioning record and a succeeded validation payment record:
  - Admin — `admin@acme.test` / `Acme#12345`
  - Users — `bob@acme.test`, `carol@acme.test` / `Acme#12345`
  - Three shifts, two of them assigned.

## API (all tenant-scoped under `/t/{slug}/api`, all secured)

| Method & path | Purpose |
| --- | --- |
| `GET /shifts?from=&to=&userId=` | Shifts for the tenant, filterable |
| `POST /shifts` | Create a shift (admin) |
| `PUT /shifts/{id}` | Update a shift (admin) |
| `DELETE /shifts/{id}` | Remove a shift (admin) |
| `POST /shifts/{id}/assign` | Assign a shift to a tenant user (admin) |
| `GET /users` | The tenant's users |
| `POST /users` | Add a member (admin; **seat-enforced**, 409 when full) |
| `POST /swaps` | Raise a swap request |
| `GET /swaps` | Swap requests for the tenant |
| `POST /swaps/{id}/approve` | Approve a swap and exchange assignments |
| `POST /swaps/{id}/reject` | Reject a swap |
| `GET /dashboard` | Assigned + upcoming shifts (per-user, or whole-tenant for admin) |
| `POST /webhook/stripe` | Stripe webhook (not tenant-scoped, anonymous) |

The Razor workspace views call these endpoints with `fetch`; views never query
the database directly.

## "Upcoming" window

The dashboard "upcoming" window is the **next 28 days (four weeks)**, defined by
`PlatformConstants.UpcomingWindowDays`.

## Non-functional notes

- Seat allowance enforced on the server (registration + add-user paths).
- Tenant isolation enforced via EF global query filters + route/claim checks.
- Inputs validated; APIs return clear JSON error bodies.
- Passwords hashed by Identity; member temp passwords are generated, never stored
  in plain text.
- Every API endpoint is `[Authorize]`d and tenant-checked.
- EF Core calls are async throughout.
