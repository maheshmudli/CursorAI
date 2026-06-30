using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Controllers;

[Authorize(Roles = Roles.CompanyAdmin)]
[Route("t/{tenantSlug}/users")]
public sealed class TenantUsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly PaymentService _paymentService;
    private readonly ProvisioningService _provisioningService;

    public TenantUsersController(ApplicationDbContext db, ITenantProvider tenantProvider, PaymentService paymentService, ProvisioningService provisioningService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _paymentService = paymentService;
        _provisioningService = provisioningService;
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddUserViewModel model, string tenantSlug)
    {
        if (_tenantProvider.TenantId is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["UserMessage"] = "User details were invalid.";
            return RedirectToAction("Users", "Tenant", new { tenantSlug });
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == _tenantProvider.TenantId);
        var count = await _provisioningService.CountTenantUsersAsync(tenant.Id);
        if (count >= tenant.SeatAllowance)
        {
            TempData["UserMessage"] = "Seat allowance reached. Buy a Pro seat before adding this user.";
            return RedirectToAction("Users", "Tenant", new { tenantSlug });
        }

        await _provisioningService.CreateTenantUserAsync(tenant.Id, model.FullName, model.Email, ProvisioningService.TemporaryPassword(), Roles.User);
        TempData["UserMessage"] = "User added within the current seat allowance.";
        return RedirectToAction("Users", "Tenant", new { tenantSlug });
    }

    [HttpPost("seat-intent")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SeatIntent([FromBody] PaymentIntentRequest request)
    {
        var seats = Math.Max(1, request.Seats);
        var intent = await _paymentService.CreatePaymentIntentAsync(seats * 500, "aud", $"Purchase {seats} Pro seat(s)", seats);
        return Json(new { clientSecret = intent.ClientSecret, paymentIntentId = intent.Id });
    }

    [HttpPost("buy-seats")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuySeats(SeatPurchaseViewModel model, string tenantSlug)
    {
        if (_tenantProvider.TenantId is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["UserMessage"] = "Seat purchase details were invalid.";
            return RedirectToAction("Users", "Tenant", new { tenantSlug });
        }

        var intent = await _paymentService.GetPaymentIntentAsync(model.PaymentIntentId);
        if (!_paymentService.IsSucceeded(intent))
        {
            TempData["UserMessage"] = $"Seat payment was not completed. Stripe status: {intent.Status}.";
            return RedirectToAction("Users", "Tenant", new { tenantSlug });
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == _tenantProvider.TenantId);
        tenant.Tier = TenantTier.Pro;
        tenant.SeatAllowance += model.Seats;
        _db.SeatPurchases.Add(new SeatPurchase
        {
            TenantId = tenant.Id,
            StripePaymentIntentId = intent.Id,
            Amount = intent.Amount,
            Currency = intent.Currency,
            Status = intent.Status,
            SeatsAdded = model.Seats
        });
        await _db.SaveChangesAsync();
        TempData["UserMessage"] = $"{model.Seats} seat(s) purchased.";
        return RedirectToAction("Users", "Tenant", new { tenantSlug });
    }
}
