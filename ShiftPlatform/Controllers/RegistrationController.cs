using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Options;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

[AllowAnonymous]
public class RegistrationController(
    ApplicationDbContext dbContext,
    IStripePaymentService stripePaymentService,
    IProvisioningService provisioningService,
    SignInManager<ApplicationUser> signInManager,
    IOptions<StripeOptions> stripeOptions) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.StripePublishableKey = stripeOptions.Value.PublishableKey;
        ViewBag.EnablePayments = stripeOptions.Value.EnablePayments;
        return View(new RegisterCompanyViewModel
        {
            Users = new List<RegisterUserInput> { new() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegisterCompanyViewModel model)
    {
        ViewBag.StripePublishableKey = stripeOptions.Value.PublishableKey;
        ViewBag.EnablePayments = stripeOptions.Value.EnablePayments;

        model.Users = model.Users.Where(x => !string.IsNullOrWhiteSpace(x.Email)).ToList();
        if (model.Users.Count + 1 > 5)
        {
            ModelState.AddModelError(string.Empty, "Free tier supports a maximum of five users including admin.");
        }
        if (stripeOptions.Value.EnablePayments && string.IsNullOrWhiteSpace(model.StripePaymentMethodId))
        {
            ModelState.AddModelError(string.Empty, "Payment method is required when payments are enabled.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var submission = new RegistrationSubmission
        {
            Id = Guid.NewGuid(),
            CompanyName = model.CompanyName,
            AdminFullName = model.AdminFullName,
            AdminEmail = model.AdminEmail,
            RequestedUserCount = model.Users.Count + 1,
            Status = RegistrationStatus.Submitted
        };
        dbContext.RegistrationSubmissions.Add(submission);
        await dbContext.SaveChangesAsync();

        try
        {
            var paymentIntent = await stripePaymentService.CreateAndConfirmPaymentIntentAsync(
                amount: 100,
                currency: "aud",
                paymentMethodId: model.StripePaymentMethodId,
                metadata: new Dictionary<string, string>
                {
                    ["purpose"] = "registration_validation",
                    ["submissionId"] = submission.Id.ToString()
                });

            dbContext.PaymentRecords.Add(new PaymentRecord
            {
                Id = Guid.NewGuid(),
                RegistrationSubmissionId = submission.Id,
                StripePaymentIntentId = paymentIntent.Id,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency,
                Status = paymentIntent.Status,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                submission.Status = RegistrationStatus.PaymentFailed;
                submission.ErrorMessage = $"Validation payment failed with status '{paymentIntent.Status}'.";
                await dbContext.SaveChangesAsync();
                ModelState.AddModelError(string.Empty, "Payment did not succeed. Nothing was provisioned.");
                return View(model);
            }

            var provisioned = await provisioningService.ProvisionTenantAsync(submission, model, paymentIntent.Id);
            if (!provisioned.Success || provisioned.Admin is null || provisioned.Tenant is null)
            {
                submission.Status = RegistrationStatus.PaymentFailed;
                submission.ErrorMessage = provisioned.Error ?? "Provisioning failed after payment.";
                await dbContext.SaveChangesAsync();
                ModelState.AddModelError(string.Empty, "Payment succeeded but provisioning failed. Contact support.");
                return View(model);
            }

            await signInManager.SignInAsync(provisioned.Admin, isPersistent: false);
            return RedirectToAction("Index", "Workspace", new { tenantSlug = provisioned.Tenant.Slug });
        }
        catch (Exception ex)
        {
            submission.Status = RegistrationStatus.PaymentFailed;
            submission.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Payment failed. Nothing was created.");
            return View(model);
        }
    }
}
