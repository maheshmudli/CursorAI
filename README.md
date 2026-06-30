# Multi-Tenant Shift Management Platform

Single deployable ASP.NET Core MVC application that hosts:

- Parent platform (registration, provisioning, payments, owner tracking)
- Tenant workspaces (`/t/{company-slug}`) for shift operations

## Stack

- ASP.NET Core MVC + API controllers (JSON for shift operations)
- SQL Server + Entity Framework Core (code-first + migrations)
- ASP.NET Core Identity + role-based authorization
- Stripe Payment Intents (`Stripe.net`)
- Razor + Bootstrap 5

## Tenant Routing Choice

This implementation uses **path routing**:

- `https://yourapp.com/t/{company-slug}`

Subdomain routing is not used because wildcard DNS/cert setup is usually environment-specific.

## Five-User Cap Interpretation

The base plan includes **five total users including the admin**:

- 1 company admin + up to 4 additional users at registration
- Server-side checks enforce this cap

## Roles

- `PlatformOwner`: sees registration/provisioning/payment/seat/access tracking
- `CompanyAdmin`: manages tenant users/seats/shifts/swap approvals
- `User`: sees own assignments, requests swaps

## Core Tenant Isolation

- Tenant-owned records carry `TenantId`
- Global query filters are applied to tenant-owned entities in `ApplicationDbContext`
- Tenant resolved from path by `TenantResolutionMiddleware`
- All tenant APIs validate route slug against resolved tenant context

## Stripe Flows

### Registration validation payment (A$1.00)

- Registration page uses Stripe Elements to collect card details
- Server confirms Payment Intent for `amount=100`, `currency=aud`
- Workspace provisioning runs only on succeeded payment
- Failed/abandoned payment does not provision tenant data

### Seat upgrades (A$5.00 per seat)

- Company starts with seat allowance `5`
- Attempting to add users past allowance is blocked server-side
- Seat purchase endpoint charges `500 * seatCount` (AUD cents)
- On success, allowance increases and tier becomes `Pro`
- Seat purchases are one-off charges (not recurring subscription)

### Webhook

- Endpoint: `POST /api/stripe/webhook`
- Validates signature via `Stripe:WebhookSecret`
- Updates final payment statuses and applies seat upgrades idempotently

## Configuration

Set in `appsettings.Development.json`, `appsettings.json`, or user secrets:

- `ConnectionStrings:DefaultConnection`
- `Stripe:SecretKey`
- `Stripe:PublishableKey`
- `Stripe:WebhookSecret`

Example local SQL Server connection:

- `Server=localhost,1433;Database=ShiftPlatformDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True`

## Setup

1. Restore packages:
   - `dotnet restore ShiftPlatform.sln`
2. Apply migrations:
   - `dotnet ef database update --project ShiftPlatform/ShiftPlatform.csproj --startup-project ShiftPlatform/ShiftPlatform.csproj`
3. Run app:
   - `dotnet run --project ShiftPlatform/ShiftPlatform.csproj`

## Seeded Accounts and Data

Seed runs at startup and creates:

- Platform owner:
  - `owner@platform.local` / `Owner#12345`
- Sample tenant:
  - slug: `sampleco`
  - admin: `admin@sampleco.local` / `Admin#12345`
  - users:
    - `user1@sampleco.local` / `User#12345`
    - `user2@sampleco.local` / `User#12345`
  - sample shifts + assignments

## Key Endpoints (Tenant Scoped)

- `GET /t/{tenantSlug}/api/shifts`
- `POST /t/{tenantSlug}/api/shifts`
- `PUT /t/{tenantSlug}/api/shifts/{id}`
- `DELETE /t/{tenantSlug}/api/shifts/{id}`
- `POST /t/{tenantSlug}/api/shifts/{id}/assign`
- `GET /t/{tenantSlug}/api/users`
- `POST /t/{tenantSlug}/api/users`
- `POST /t/{tenantSlug}/api/users/purchase-seats`
- `POST /t/{tenantSlug}/api/swaps`
- `GET /t/{tenantSlug}/api/swaps`
- `POST /t/{tenantSlug}/api/swaps/{id}/approve`
- `POST /t/{tenantSlug}/api/swaps/{id}/reject`
- `GET /t/{tenantSlug}/api/dashboard` (next 4 weeks window = 28 days)

## Notes

- Payment validation charge is currently captured by default.
- To refund immediately, call Stripe Refund API after successful provisioning.
- To move to recurring monthly seat billing later, replace seat purchase intents with Stripe Subscriptions and per-seat quantities.
