using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Workspace
{
    [Authorize(Roles = "CompanyAdmin")]
    public class SeatsController : WorkspaceBaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStripeService _stripeService;
        private readonly IConfiguration _configuration;

        public SeatsController(ApplicationDbContext db, ITenantContext tenantContext,
            UserManager<ApplicationUser> userManager, IStripeService stripeService, IConfiguration configuration)
            : base(db, tenantContext)
        {
            _userManager = userManager;
            _stripeService = stripeService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Upgrade(string? pendingUserFullName = null, string? pendingUserEmail = null)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId!.Value);
            if (tenant == null) return NotFound();

            var userCount = await _db.Users.IgnoreQueryFilters()
                .CountAsync(u => u.TenantId == tenant.Id);

            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];

            return View(new SeatUpgradeViewModel
            {
                CurrentSeatAllowance = tenant.SeatAllowance,
                CurrentUserCount = userCount,
                SeatsToAdd = 1,
                CostPerSeat = 500,
                PendingUserFullName = pendingUserFullName,
                PendingUserEmail = pendingUserEmail
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSeatPaymentIntent([FromBody] SeatPaymentIntentRequest request)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId!.Value);
            if (tenant == null) return NotFound();

            var seatsToAdd = Math.Max(1, request.SeatsToAdd);
            var amount = 500L * seatsToAdd;

            try
            {
                var metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenant.Id.ToString(),
                    ["tenant_slug"] = tenant.Slug,
                    ["seats_to_add"] = seatsToAdd.ToString()
                };
                var paymentIntent = await _stripeService.CreatePaymentIntentAsync(amount, "aud", metadata);
                return Json(new { clientSecret = paymentIntent.ClientSecret, paymentIntentId = paymentIntent.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteUpgrade(SeatUpgradeViewModel model)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId!.Value);
            if (tenant == null) return NotFound();

            if (string.IsNullOrEmpty(model.PaymentIntentId))
            {
                ModelState.AddModelError(string.Empty, "Payment is required.");
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                return View("Upgrade", model);
            }

            // Verify payment
            Stripe.PaymentIntent? paymentIntent = null;
            try
            {
                paymentIntent = await _stripeService.RetrievePaymentIntentAsync(model.PaymentIntentId);
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Unable to verify payment.");
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                return View("Upgrade", model);
            }

            if (paymentIntent.Status != "succeeded" && paymentIntent.Status != "requires_capture")
            {
                ModelState.AddModelError(string.Empty, $"Payment not successful (status: {paymentIntent.Status}).");
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                return View("Upgrade", model);
            }

            var seatsToAdd = Math.Max(1, model.SeatsToAdd);

            // Record seat purchase
            _db.SeatPurchases.Add(new SeatPurchase
            {
                TenantId = tenant.Id,
                StripePaymentIntentId = model.PaymentIntentId,
                Amount = 500L * seatsToAdd,
                Currency = "aud",
                Status = PaymentStatus.Succeeded,
                SeatsAdded = seatsToAdd,
                CreatedAt = DateTime.UtcNow
            });

            // Update seat allowance and tier
            tenant.SeatAllowance += seatsToAdd;
            if (tenant.Tier == TenantTier.Base)
            {
                tenant.Tier = TenantTier.Pro;
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully purchased {seatsToAdd} seat(s). Your new allowance is {tenant.SeatAllowance}.";

            // If there was a pending user, redirect to add them
            if (!string.IsNullOrEmpty(model.PendingUserEmail) && !string.IsNullOrEmpty(model.PendingUserFullName))
            {
                return RedirectToAction("AddUser", "WorkspaceHome",
                    new { area = "", slug = ViewBag.TenantSlug, fullName = model.PendingUserFullName, email = model.PendingUserEmail });
            }

            return RedirectToAction("Users", "WorkspaceHome");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(AddUserViewModel model)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId!.Value);
            if (tenant == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                return View(model);
            }

            var currentUserCount = await _db.Users.IgnoreQueryFilters()
                .CountAsync(u => u.TenantId == tenant.Id);

            if (currentUserCount >= tenant.SeatAllowance)
            {
                return RedirectToAction("Upgrade", new { pendingUserFullName = model.FullName, pendingUserEmail = model.Email });
            }

            // Check for duplicate email
            if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            var tempPassword = GenerateTemporaryPassword();
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                TenantId = tenant.Id,
                EmailConfirmed = true,
                IsActive = true,
                TemporaryPassword = tempPassword,
                MustChangePassword = true
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }
            await _userManager.AddToRoleAsync(user, "User");

            TempData["SuccessMessage"] = $"User {model.FullName} added. Temporary password: {tempPassword}";
            return RedirectToAction("Users", "WorkspaceHome");
        }

        [HttpGet]
        public IActionResult AddUser(string? fullName = null, string? email = null)
        {
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            return View(new AddUserViewModel { FullName = fullName ?? "", Email = email ?? "" });
        }

        private static string GenerateTemporaryPassword()
        {
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
            var rng = new Random();
            var password = new char[12];
            password[0] = "ABCDEFGHJKLMNPQRSTUVWXYZ"[rng.Next(24)];
            password[1] = "abcdefghijkmnopqrstuvwxyz"[rng.Next(25)];
            password[2] = "23456789"[rng.Next(8)];
            password[3] = "!@#$"[rng.Next(4)];
            for (int i = 4; i < 12; i++)
                password[i] = chars[rng.Next(chars.Length)];
            return new string(password.OrderBy(_ => rng.Next()).ToArray());
        }
    }

    public class SeatPaymentIntentRequest
    {
        public int SeatsToAdd { get; set; } = 1;
    }
}
