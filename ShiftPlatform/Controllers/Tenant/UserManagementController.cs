using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.ViewModels;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Tenant;

[Authorize(Roles = Constants.CompanyAdminRole)]
[Route($"{Constants.TenantRoutePrefix}/{{tenantSlug}}/users")]
public class UserManagementController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISeatService _seatService;
    private readonly ITenantContext _tenantContext;
    private readonly StripeSettings _stripeSettings;

    public UserManagementController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISeatService seatService,
        ITenantContext tenantContext,
        IOptions<StripeSettings> stripeSettings)
    {
        _db = db;
        _userManager = userManager;
        _seatService = seatService;
        _tenantContext = tenantContext;
        _stripeSettings = stripeSettings.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string tenantSlug)
    {
        if (!await ValidateAccess(tenantSlug)) return Forbid();

        var tenant = await _db.Tenants.FindAsync(_tenantContext.TenantId!.Value);
        var users = await _db.Users
            .Where(u => u.TenantId == tenant!.Id)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        ViewBag.TenantSlug = tenantSlug;
        ViewBag.SeatAllowance = tenant!.SeatAllowance;
        ViewBag.UserCount = users.Count;
        ViewBag.StripePublishableKey = _stripeSettings.PublishableKey;
        ViewBag.CanAddUser = users.Count < tenant.SeatAllowance;

        return View(users);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string tenantSlug, AddUserViewModel model)
    {
        if (!await ValidateAccess(tenantSlug)) return Forbid();

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index), new { tenantSlug });
        }

        var tenantId = _tenantContext.TenantId!.Value;
        if (!await _seatService.CanAddUserAsync(tenantId))
        {
            TempData["Error"] = "Seat limit reached. Purchase additional seats to add more users.";
            return RedirectToAction(nameof(Index), new { tenantSlug });
        }

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            TempData["Error"] = "A user with this email already exists.";
            return RedirectToAction(nameof(Index), new { tenantSlug });
        }

        var tempPassword = Guid.NewGuid().ToString("N")[..12] + "Aa1!";
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            TenantId = tenantId,
            EmailConfirmed = true,
            TemporaryPassword = tempPassword
        };

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index), new { tenantSlug });
        }

        await _userManager.AddToRoleAsync(user, Constants.UserRole);
        TempData["Success"] = $"User created. Temporary password: {tempPassword}";
        return RedirectToAction(nameof(Index), new { tenantSlug });
    }

    [HttpPost("buy-seats")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuySeats(string tenantSlug, BuySeatsViewModel model)
    {
        if (!await ValidateAccess(tenantSlug)) return Forbid();

        var (purchase, intent) = await _seatService.StartSeatPurchaseAsync(_tenantContext.TenantId!.Value, model.SeatCount);
        return View("BuySeats", new SeatPurchasePaymentViewModel
        {
            TenantSlug = tenantSlug,
            PurchaseId = purchase.Id,
            SeatCount = model.SeatCount,
            ClientSecret = intent.ClientSecret,
            StripePublishableKey = _stripeSettings.PublishableKey
        });
    }

    [HttpPost("complete-seat-purchase")]
    public async Task<IActionResult> CompleteSeatPurchase(string tenantSlug, [FromBody] CompleteSeatPurchaseViewModel model)
    {
        if (!await ValidateAccess(tenantSlug)) return Forbid();

        var (success, error) = await _seatService.CompleteSeatPurchaseAsync(
            _tenantContext.TenantId!.Value, model.PurchaseId, model.PaymentIntentId);

        if (!success)
        {
            return BadRequest(new ApiError { Message = error ?? "Payment failed." });
        }

        return Ok(new { redirectUrl = $"/{Constants.TenantRoutePrefix}/{tenantSlug}/users" });
    }

    private async Task<bool> ValidateAccess(string tenantSlug)
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantSlug != tenantSlug) return false;
        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }
}

public class SeatPurchasePaymentViewModel
{
    public string TenantSlug { get; set; } = string.Empty;
    public int PurchaseId { get; set; }
    public int SeatCount { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
    public string StripePublishableKey { get; set; } = string.Empty;
}
