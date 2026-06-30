# Multi-Tenant Shift Management Platform

Single deployable ASP.NET Core MVC application for a parent registration platform and route-isolated tenant shift workspaces.

## Stack

- ASP.NET Core MVC with API controllers for tenant shift data
- SQL Server with Entity Framework Core code-first migrations
- ASP.NET Core Identity with roles: `Platform Owner`, `Company Admin`, `User`
- Stripe Payment Intents through the official Stripe .NET SDK
- Razor views with Bootstrap 5

## Tenant routing and isolation

This build uses path routing because wildcard subdomain setup is environment-specific:

- Parent platform: `/`
- Tenant workspace: `/t/{company-slug}`
- Tenant APIs: `/t/{company-slug}/api/...`

Every tenant-owned shift record carries `TenantId`. `ApplicationDbContext` applies EF Core global query filters to `ApplicationUser`, `Shift`, `ShiftAssignment`, and `SwapRequest` using the tenant resolved from the current route. Platform Owner reporting intentionally uses `IgnoreQueryFilters()` for parent-level tracking only.

## Seat and billing decisions

The five included seats are five total users, and the registering Company Admin counts as one of those seats. Registration therefore accepts up to four additional users.

Seat upgrades are implemented as one-off Stripe PaymentIntent charges of 5.00 AUD per extra seat. To switch to monthly recurring billing later, replace the seat PaymentIntent creation in `PaymentService`/`TenantUsersController` with Stripe Prices and Subscriptions, then update the webhook handler to react to subscription lifecycle events.

The 1.00 AUD registration validation charge is captured by default. To avoid keeping it, create the PaymentIntent with manual capture and cancel it after validation, or call Stripe refunds after a successful validation webhook.

## Configuration

Set these values in `appsettings.Development.json`, environment variables, or user secrets:

- `ConnectionStrings:DefaultConnection`
- `Stripe:PublishableKey`
- `Stripe:SecretKey`
- `Stripe:WebhookSecret`

Example user-secret commands:

- `dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."`
- `dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."`
- `dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."`

## Database

Run migrations against SQL Server:

- `dotnet ef database update`

The app also calls `Database.MigrateAsync()` at startup and seeds development data.

## Seeded accounts

All seeded users use password `Password1!`.

- Platform Owner: `owner@example.com`
- Sample Company Admin: `admin@sample.test`
- Sample Users: `alex@sample.test`, `casey@sample.test`
- Sample workspace: `/t/sample-co`

## Stripe webhook

Point Stripe CLI or the Stripe dashboard webhook endpoint at:

- `/stripe/webhook`

The webhook verifies `Stripe:WebhookSecret` when configured and updates stored registration validation payment and seat purchase statuses from PaymentIntent events. Registration and seat-purchase form posts also retrieve the PaymentIntent server-side and only provision or increase seats when Stripe reports `succeeded`.

## Key flows

- Registration creates a `RegistrationSubmission`, validates the 1.00 AUD PaymentIntent, then provisions the tenant, admin, users, template shifts, `ProvisioningRecord`, and `PaymentRecord`.
- Failed or abandoned payments create no tenant and no workspace.
- Adding a user is blocked server-side when tenant user count reaches `SeatAllowance`.
- Buying seats charges 5.00 AUD per seat, moves the tenant to Pro, records `SeatPurchase`, and increases `SeatAllowance`.
- Shift CRUD, assignment, swaps, and dashboard data are all served from secured tenant-scoped JSON APIs.

## Useful commands

- `dotnet restore`
- `dotnet build`
- `dotnet ef migrations add <Name>`
- `dotnet ef database update`
- `dotnet run`
