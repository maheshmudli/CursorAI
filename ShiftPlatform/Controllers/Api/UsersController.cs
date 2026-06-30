using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Contracts;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

[ApiController]
[Authorize(Roles = $"{ApplicationRoles.CompanyAdmin},{ApplicationRoles.User}")]
[Route("t/{tenantSlug}/api/users")]
public class UsersController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IStripePaymentService stripePaymentService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string tenantSlug)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var users = await dbContext.Users
            .Where(u => u.TenantId == tenantContext.CurrentTenantId)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> Post(string tenantSlug, [FromBody] CreateUserRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tenant = await dbContext.Tenants.FirstAsync(t => t.Id == tenantContext.CurrentTenantId);
        var currentCount = await dbContext.Users.CountAsync(u => u.TenantId == tenant.Id);
        if (currentCount >= tenant.SeatAllowance)
        {
            return BadRequest(new
            {
                error = "Seat allowance reached. Buy seats to add more users.",
                tenant.SeatAllowance,
                currentCount
            });
        }

        var newUser = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TenantId = tenant.Id,
            EmailConfirmed = true
        };

        var tempPassword = $"Tmp!{Guid.NewGuid():N}"[..12] + "aA1!";
        var result = await userManager.CreateAsync(newUser, tempPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        await userManager.AddToRoleAsync(newUser, ApplicationRoles.User);
        return Ok(new { newUser.Id, TemporaryPassword = tempPassword });
    }

    [HttpPost("purchase-seats")]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> PurchaseSeats(string tenantSlug, [FromBody] PurchaseSeatsRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var paymentIntent = await stripePaymentService.CreateAndConfirmPaymentIntentAsync(
            amount: request.SeatCount * 500L,
            currency: "aud",
            paymentMethodId: request.StripePaymentMethodId,
            metadata: new Dictionary<string, string>
            {
                ["purpose"] = "seat_purchase",
                ["tenantId"] = tenantContext.CurrentTenantId!.Value.ToString(),
                ["seats"] = request.SeatCount.ToString()
            });

        var purchase = new SeatPurchase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.CurrentTenantId!.Value,
            StripePaymentIntentId = paymentIntent.Id,
            Amount = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            Status = paymentIntent.Status,
            SeatsAdded = request.SeatCount,
            SeatsApplied = false
        };

        dbContext.SeatPurchases.Add(purchase);

        if (string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var tenant = await dbContext.Tenants.FirstAsync(t => t.Id == tenantContext.CurrentTenantId);
            tenant.SeatAllowance += request.SeatCount;
            tenant.Tier = TenantTier.Pro;
            purchase.SeatsApplied = true;
        }

        await dbContext.SaveChangesAsync();
        return Ok(new
        {
            paymentIntentId = paymentIntent.Id,
            paymentIntent.Status,
            purchase.SeatsAdded
        });
    }

    private bool IsTenantMatch(string tenantSlug) =>
        tenantContext.IsTenantRoute &&
        string.Equals(tenantContext.CurrentTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase);
}
