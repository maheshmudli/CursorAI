using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Controllers;

public sealed class RegistrationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PaymentService _paymentService;
    private readonly ProvisioningService _provisioningService;

    public RegistrationController(ApplicationDbContext db, PaymentService paymentService, ProvisioningService provisioningService)
    {
        _db = db;
        _paymentService = paymentService;
        _provisioningService = provisioningService;
    }

    [HttpGet]
    public IActionResult Index() => View(new RegistrationViewModel());

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreatePaymentIntent()
    {
        var intent = await _paymentService.CreatePaymentIntentAsync(100, "aud", "Workspace validation charge");
        return Json(new { clientSecret = intent.ClientSecret, paymentIntentId = intent.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegistrationViewModel model)
    {
        var activeUsers = model.Users.Where(u => !string.IsNullOrWhiteSpace(u.Email)).ToList();
        model.Users = activeUsers.Concat(Enumerable.Range(0, Math.Max(0, 4 - activeUsers.Count)).Select(_ => new RegistrationUserViewModel())).ToList();

        var suppliedEmails = activeUsers.Select(u => u.Email).Append(model.AdminEmail).ToList();
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => suppliedEmails.Contains(u.Email!)))
        {
            ModelState.AddModelError(string.Empty, "One or more supplied email addresses already belong to an account.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var submission = new RegistrationSubmission
        {
            CompanyName = model.CompanyName,
            AdminName = model.AdminFullName,
            AdminEmail = model.AdminEmail,
            RequestedUserCount = activeUsers.Count + 1
        };
        _db.RegistrationSubmissions.Add(submission);

        var intent = await _paymentService.GetPaymentIntentAsync(model.PaymentIntentId);
        if (!_paymentService.IsSucceeded(intent))
        {
            submission.Status = RegistrationStatus.PaymentFailed;
            submission.FailureReason = $"Stripe PaymentIntent status was {intent.Status}.";
            _db.PaymentRecords.Add(new PaymentRecord
            {
                StripePaymentIntentId = intent.Id,
                Amount = intent.Amount,
                Currency = intent.Currency,
                Status = intent.Status,
                Purpose = PaymentPurpose.RegistrationValidation
            });
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "The validation payment was not completed. No workspace was created.");
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var tenant = await _provisioningService.ProvisionTenantAsync(model, intent.Id, intent.Status);
        submission.TenantId = tenant.Id;
        submission.Status = RegistrationStatus.Provisioned;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return RedirectToAction("Login", "Account");
    }
}
