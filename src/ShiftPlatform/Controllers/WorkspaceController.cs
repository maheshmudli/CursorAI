using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

/// <summary>
/// The provisioned shift-management workspace (Razor views). Views call the
/// tenant-scoped JSON API; the controller itself only renders pages and drives
/// the Pro-license seat-purchase payment flow. Tenant access is enforced by the
/// tenant-resolution middleware.
/// </summary>
[Authorize(Roles = PlatformConstants.Roles.CompanyAdmin + "," + PlatformConstants.Roles.User)]
[Route("t/{slug}/workspace")]
public class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPaymentGateway _gateway;
    private readonly ISeatService _seatService;

    public WorkspaceController(ApplicationDbContext db, ITenantContext tenant, IPaymentGateway gateway, ISeatService seatService)
    {
        _db = db;
        _tenant = tenant;
        _gateway = gateway;
        _seatService = seatService;
    }

    private async Task<Tenant> CurrentTenantAsync()
        => await _db.Tenants.FirstAsync(t => t.Id == _tenant.TenantId);

    private void SetCommonViewData(Tenant tenant)
    {
        ViewBag.Slug = tenant.Slug;
        ViewBag.CompanyName = tenant.CompanyName;
        ViewBag.Tier = tenant.Tier.ToString();
        ViewBag.SeatAllowance = tenant.SeatAllowance;
        ViewBag.IsAdmin = User.IsInRole(PlatformConstants.Roles.CompanyAdmin);
        ViewBag.UserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int welcome = 0)
    {
        var tenant = await CurrentTenantAsync();
        SetCommonViewData(tenant);
        ViewBag.Welcome = welcome == 1;
        return View();
    }

    [HttpGet("shifts")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    public async Task<IActionResult> Shifts()
    {
        var tenant = await CurrentTenantAsync();
        SetCommonViewData(tenant);
        return View();
    }

    [HttpGet("swaps")]
    public async Task<IActionResult> Swaps()
    {
        var tenant = await CurrentTenantAsync();
        SetCommonViewData(tenant);
        return View();
    }

    [HttpGet("users")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    public async Task<IActionResult> Users()
    {
        var tenant = await CurrentTenantAsync();
        SetCommonViewData(tenant);
        ViewBag.UserCount = await _db.Users.CountAsync(u => u.TenantId == tenant.Id);
        return View();
    }

    // ---- Pro license seat purchase flow ----

    [HttpPost("buy-seats")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuySeats(int seats = 1)
    {
        var tenant = await CurrentTenantAsync();
        if (seats < 1) seats = 1;
        var (purchase, _) = await _seatService.CreateSeatPurchaseAsync(tenant.Id, seats);
        return RedirectToAction(nameof(SeatPayment), new { slug = tenant.Slug, purchaseId = purchase.Id });
    }

    [HttpGet("seat-payment/{purchaseId:int}")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    public async Task<IActionResult> SeatPayment(int purchaseId)
    {
        var tenant = await CurrentTenantAsync();
        var purchase = await _db.SeatPurchases.FirstOrDefaultAsync(s => s.Id == purchaseId && s.TenantId == tenant.Id);
        if (purchase == null) return NotFound();

        var intent = await _gateway.GetPaymentIntentAsync(purchase.StripePaymentIntentId);
        SetCommonViewData(tenant);
        var model = new PaymentPageModel
        {
            CompanyName = tenant.CompanyName,
            AmountCents = purchase.Amount,
            Currency = purchase.Currency,
            PaymentIntentId = purchase.StripePaymentIntentId,
            ClientSecret = intent.ClientSecret ?? string.Empty,
            PublishableKey = _gateway.PublishableKey,
            IsLiveStripe = _gateway.IsLive,
            Purpose = "seats",
            Slug = tenant.Slug,
            Seats = purchase.SeatsAdded
        };
        ViewBag.PurchaseId = purchase.Id;
        return View("~/Views/Workspace/SeatPayment.cshtml", model);
    }

    [HttpPost("confirm-test-seat/{purchaseId:int}")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTestSeat(int purchaseId, bool success)
    {
        if (_gateway.IsLive) return BadRequest("Not available against live Stripe.");
        var tenant = await CurrentTenantAsync();
        var purchase = await _db.SeatPurchases.FirstOrDefaultAsync(s => s.Id == purchaseId && s.TenantId == tenant.Id);
        if (purchase == null) return NotFound();
        var result = await _gateway.ConfirmTestPaymentAsync(purchase.StripePaymentIntentId, success);
        return Json(new { status = result.Status });
    }

    [HttpPost("complete-seat/{purchaseId:int}")]
    [Authorize(Roles = PlatformConstants.Roles.CompanyAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteSeat(int purchaseId)
    {
        var tenant = await CurrentTenantAsync();
        var purchase = await _db.SeatPurchases.FirstOrDefaultAsync(s => s.Id == purchaseId && s.TenantId == tenant.Id);
        if (purchase == null) return NotFound();

        var intent = await _gateway.GetPaymentIntentAsync(purchase.StripePaymentIntentId);
        if (intent.Status != "succeeded")
        {
            TempData["SeatError"] = "The seat payment was not completed, so no seats were added.";
            return RedirectToAction(nameof(Users), new { slug = tenant.Slug });
        }

        await _seatService.ApplySeatPurchaseAsync(purchase.StripePaymentIntentId, intent.Status);
        TempData["SeatSuccess"] = $"Payment received. {purchase.SeatsAdded} seat(s) added — you can now add more users.";
        return RedirectToAction(nameof(Users), new { slug = tenant.Slug });
    }
}
