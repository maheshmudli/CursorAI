using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.ViewModels;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Platform;

public class RegistrationController : Controller
{
    private readonly IRegistrationService _registrationService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly StripeSettings _stripeSettings;

    public RegistrationController(
        IRegistrationService registrationService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOptions<StripeSettings> stripeSettings)
    {
        _registrationService = registrationService;
        _signInManager = signInManager;
        _userManager = userManager;
        _stripeSettings = stripeSettings.Value;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.StripePublishableKey = _stripeSettings.PublishableKey;
        return View(new RegistrationViewModel
        {
            AdditionalUsers = []
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(RegistrationViewModel model)
    {
        ViewBag.StripePublishableKey = _stripeSettings.PublishableKey;

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var totalUsers = _registrationService.CountTotalUsers(model);
        if (totalUsers > Infrastructure.Constants.IncludedSeats)
        {
            ModelState.AddModelError(string.Empty,
                $"You can register at most {Infrastructure.Constants.IncludedSeats} users total (you plus additional team members).");
            return View("Index", model);
        }

        try
        {
            var (submission, intent) = await _registrationService.StartRegistrationAsync(model);
            HttpContext.Session.SetInt32("RegistrationSubmissionId", submission.Id);
            HttpContext.Session.SetString("RegistrationAdminPassword", model.AdminPassword);
            return View("Payment", new RegistrationPaymentViewModel
            {
                SubmissionId = submission.Id,
                ClientSecret = intent.ClientSecret,
                CompanyName = model.CompanyName,
                StripePublishableKey = _stripeSettings.PublishableKey
            });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Complete([FromBody] CompleteRegistrationViewModel model)
    {
        var sessionPassword = HttpContext.Session.GetString("RegistrationAdminPassword");
        if (!string.IsNullOrEmpty(sessionPassword))
        {
            model.AdminPassword = sessionPassword;
        }

        var result = await _registrationService.CompleteRegistrationAsync(model);
        if (!result.Success)
        {
            return BadRequest(new ApiError { Message = result.Error ?? "Registration failed." });
        }

        HttpContext.Session.Remove("RegistrationAdminPassword");
        HttpContext.Session.Remove("RegistrationSubmissionId");

        var submission = await HttpContext.RequestServices
            .GetRequiredService<Data.ApplicationDbContext>()
            .RegistrationSubmissions.FindAsync(model.SubmissionId);
        if (submission != null)
        {
            var admin = await _userManager.FindByEmailAsync(submission.AdminEmail);
            if (admin != null)
            {
                await _signInManager.SignInAsync(admin, isPersistent: false);
            }
        }

        return Ok(new { redirectUrl = result.RedirectUrl });
    }
}

public class RegistrationPaymentViewModel
{
    public int SubmissionId { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string StripePublishableKey { get; set; } = string.Empty;
}
