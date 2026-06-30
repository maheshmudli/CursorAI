using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.ViewModels;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Platform
{
    public class RegistrationController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStripeService _stripeService;
        private readonly ITenantProvisioningService _provisioningService;
        private readonly IConfiguration _configuration;

        public RegistrationController(
            ApplicationDbContext db,
            IStripeService stripeService,
            ITenantProvisioningService provisioningService,
            IConfiguration configuration)
        {
            _db = db;
            _stripeService = stripeService;
            _provisioningService = provisioningService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new RegistrationViewModel();
            model.AdditionalUsers.Add(new UserEntryViewModel());
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    ["company_name"] = request.CompanyName ?? "Unknown",
                    ["admin_email"] = request.AdminEmail ?? "Unknown"
                };

                var paymentIntent = await _stripeService.CreatePaymentIntentAsync(100, "aud", metadata);

                // Record the registration attempt
                var attempt = new RegistrationAttempt
                {
                    CompanyName = request.CompanyName ?? "",
                    AdminEmail = request.AdminEmail ?? "",
                    Status = RegistrationStatus.PaymentPending,
                    StripePaymentIntentId = paymentIntent.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _db.RegistrationAttempts.Add(attempt);
                await _db.SaveChangesAsync();

                return Json(new { clientSecret = paymentIntent.ClientSecret, paymentIntentId = paymentIntent.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(RegistrationViewModel model)
        {
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];

            // Remove empty users
            model.AdditionalUsers = model.AdditionalUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.FullName) && !string.IsNullOrWhiteSpace(u.Email))
                .ToList();

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // Validate total user count (admin + additional users <= 5)
            // Interpretation: 5 users total including admin, so up to 4 additional users
            if (model.AdditionalUsers.Count > 4)
            {
                ModelState.AddModelError(string.Empty, "Free tier allows up to 5 users total (including admin). You can add up to 4 additional users.");
                return View("Index", model);
            }

            // Check for duplicate emails
            var allEmails = new List<string> { model.AdminEmail };
            allEmails.AddRange(model.AdditionalUsers.Select(u => u.Email));
            if (allEmails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allEmails.Count)
            {
                ModelState.AddModelError(string.Empty, "Duplicate email addresses are not allowed.");
                return View("Index", model);
            }

            // Check existing email
            if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == model.AdminEmail))
            {
                ModelState.AddModelError("AdminEmail", "An account with this email already exists.");
                return View("Index", model);
            }

            // Verify payment
            if (string.IsNullOrEmpty(model.PaymentIntentId))
            {
                ModelState.AddModelError(string.Empty, "Payment is required to complete registration.");
                return View("Index", model);
            }

            PaymentIntent? paymentIntent = null;
            try
            {
                paymentIntent = await _stripeService.RetrievePaymentIntentAsync(model.PaymentIntentId);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Unable to verify payment. Please try again.");
                await UpdateRegistrationAttemptStatus(model.PaymentIntentId, RegistrationStatus.PaymentFailed, "Unable to retrieve payment intent");
                return View("Index", model);
            }

            if (paymentIntent.Status != "succeeded" && paymentIntent.Status != "requires_capture")
            {
                ModelState.AddModelError(string.Empty, $"Payment was not successful (status: {paymentIntent.Status}). Please try again.");
                await UpdateRegistrationAttemptStatus(model.PaymentIntentId, RegistrationStatus.PaymentFailed, paymentIntent.Status);
                return View("Index", model);
            }

            try
            {
                // Provision the tenant
                var additionalUsers = model.AdditionalUsers.Select(u => (u.FullName, u.Email)).ToList();
                var (tenant, adminPassword, createdUsers) = await _provisioningService.ProvisionTenantAsync(
                    model.CompanyName,
                    model.AdminFullName,
                    model.AdminEmail,
                    model.AdminPassword,
                    additionalUsers,
                    model.PaymentIntentId);

                // Record payment against tenant
                _db.PaymentRecords.Add(new PaymentRecord
                {
                    TenantId = tenant.Id,
                    StripePaymentIntentId = model.PaymentIntentId,
                    Amount = 100,
                    Currency = "aud",
                    Status = PaymentStatus.Succeeded,
                    CreatedAt = DateTime.UtcNow,
                    Purpose = "registration"
                });

                // Update registration attempt
                await UpdateRegistrationAttemptStatus(model.PaymentIntentId, RegistrationStatus.Completed, null, tenant.Id);

                await _db.SaveChangesAsync();

                // Prepare result view
                var result = new RegistrationResultViewModel
                {
                    CompanyName = tenant.CompanyName,
                    TenantSlug = tenant.Slug,
                    WorkspaceUrl = $"/t/{tenant.Slug}",
                    UserCredentials = createdUsers.Select(uc => new UserCredential
                    {
                        FullName = uc.user.FullName,
                        Email = uc.user.Email ?? "",
                        TemporaryPassword = uc.tempPassword
                    }).ToList()
                };

                return View("Success", result);
            }
            catch (Exception ex)
            {
                await UpdateRegistrationAttemptStatus(model.PaymentIntentId, RegistrationStatus.PaymentFailed, ex.Message);
                ModelState.AddModelError(string.Empty, "Registration failed. Please contact support.");
                return View("Index", model);
            }
        }

        private async Task UpdateRegistrationAttemptStatus(string paymentIntentId, RegistrationStatus status, string? failureReason, int? tenantId = null)
        {
            var attempt = await _db.RegistrationAttempts
                .FirstOrDefaultAsync(r => r.StripePaymentIntentId == paymentIntentId);
            if (attempt != null)
            {
                attempt.Status = status;
                attempt.FailureReason = failureReason;
                if (tenantId.HasValue) attempt.TenantId = tenantId;
                if (status == RegistrationStatus.Completed) attempt.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
    }

    public class CreatePaymentIntentRequest
    {
        public string? CompanyName { get; set; }
        public string? AdminEmail { get; set; }
    }
}
