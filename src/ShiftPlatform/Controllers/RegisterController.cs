using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

/// <summary>
/// Parent-platform company registration. Validation and the five-user cap are
/// enforced server side, a 1.00 AUD Stripe validation charge gates everything,
/// and only a succeeded payment triggers provisioning.
/// </summary>
public class RegisterController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IProvisioningService _provisioning;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterController> _logger;

    public RegisterController(
        ApplicationDbContext db,
        IPaymentGateway gateway,
        IProvisioningService provisioning,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RegisterController> logger)
    {
        _db = db;
        _gateway = gateway;
        _provisioning = provisioning;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = new RegistrationInput { Members = { new NewUserInput() } };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegistrationInput input)
    {
        input.Members = input.Members?
            .Where(m => !string.IsNullOrWhiteSpace(m.FullName) || !string.IsNullOrWhiteSpace(m.Email))
            .ToList() ?? new List<NewUserInput>();

        // Server-side free-tier cap: admin counts as one of the five seats, so
        // at most four additional members may be added at registration.
        var totalUsers = 1 + input.Members.Count;
        if (totalUsers > PlatformConstants.IncludedSeats)
        {
            ModelState.AddModelError(string.Empty,
                $"Registration includes {PlatformConstants.IncludedSeats} users (you plus {PlatformConstants.IncludedSeats - 1} members). " +
                "Remove members here and buy extra seats from inside your workspace once you are set up.");
        }

        var emails = new List<string> { input.AdminEmail };
        emails.AddRange(input.Members.Select(m => m.Email));
        if (emails.Where(e => !string.IsNullOrWhiteSpace(e))
                  .GroupBy(e => e.Trim().ToLowerInvariant())
                  .Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Each email address must be unique within the registration.");
        }

        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            if (await _userManager.FindByEmailAsync(email) != null)
                ModelState.AddModelError(string.Empty, $"An account already exists for {email}.");
        }

        if (!ModelState.IsValid)
        {
            if (input.Members.Count == 0) input.Members.Add(new NewUserInput());
            return View(input);
        }

        var slug = await SlugGenerator.GenerateUniqueAsync(_db, input.CompanyName);

        var intent = await _gateway.CreatePaymentIntentAsync(
            PlatformConstants.ValidationAmountCents, PlatformConstants.Currency,
            new Dictionary<string, string> { ["purpose"] = "registration_validation", ["company"] = input.CompanyName });

        var pending = new PendingRegistration
        {
            CompanyName = input.CompanyName,
            Slug = slug,
            PayloadJson = JsonSerializer.Serialize(input),
            StripePaymentIntentId = intent.Id,
            Amount = PlatformConstants.ValidationAmountCents,
            Currency = PlatformConstants.Currency,
            Status = "Submitted"
        };
        _db.PendingRegistrations.Add(pending);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Payment), new { token = pending.Token });
    }

    [HttpGet]
    public async Task<IActionResult> Payment(Guid token)
    {
        var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Token == token);
        if (pending == null || pending.Status == "Provisioned")
            return RedirectToAction(nameof(Index));

        var intent = await _gateway.GetPaymentIntentAsync(pending.StripePaymentIntentId);
        var model = new PaymentPageModel
        {
            Token = token,
            CompanyName = pending.CompanyName,
            AmountCents = pending.Amount,
            Currency = pending.Currency,
            PaymentIntentId = pending.StripePaymentIntentId,
            ClientSecret = intent.ClientSecret ?? string.Empty,
            PublishableKey = _gateway.PublishableKey,
            IsLiveStripe = _gateway.IsLive,
            Purpose = "registration"
        };
        return View(model);
    }

    /// <summary>Dev-only: simulate the browser confirming a fake Payment Intent.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTest(Guid token, bool success)
    {
        if (_gateway.IsLive) return BadRequest("Not available against live Stripe.");
        var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Token == token);
        if (pending == null) return NotFound();
        var result = await _gateway.ConfirmTestPaymentAsync(pending.StripePaymentIntentId, success);
        return Json(new { status = result.Status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(Guid token)
    {
        var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Token == token);
        if (pending == null) return RedirectToAction(nameof(Index));

        if (pending.Status == "Provisioned" && pending.TenantId != null)
            return RedirectToWorkspace(pending.Slug);

        var intent = await _gateway.GetPaymentIntentAsync(pending.StripePaymentIntentId);
        if (intent.Status != "succeeded")
        {
            pending.Status = "Failed";
            await _db.SaveChangesAsync();
            TempData["RegistrationError"] = "The payment was not completed, so no workspace was created. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        pending.Status = "Paid";
        await _db.SaveChangesAsync();

        var input = JsonSerializer.Deserialize<RegistrationInput>(pending.PayloadJson)!;
        var result = await _provisioning.ProvisionAsync(pending, input);

        TempData["ProvisionedSlug"] = result.Tenant.Slug;
        TempData["MemberCredentials"] = JsonSerializer.Serialize(result.Members);

        await _signInManager.SignInAsync(result.Admin, isPersistent: false);
        return RedirectToWorkspace(result.Tenant.Slug);
    }

    private IActionResult RedirectToWorkspace(string slug)
        => Redirect($"/t/{slug}/workspace?welcome=1");
}
