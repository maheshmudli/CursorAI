using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftPlatform.Data;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Options;
using Stripe;

namespace ShiftPlatform.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/stripe/webhook")]
public class StripeWebhookController(
    ApplicationDbContext dbContext,
    IOptions<StripeOptions> stripeOptions) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var stripeSignature = Request.Headers["Stripe-Signature"];
        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, stripeOptions.Value.WebhookSecret);
        }
        catch (Exception)
        {
            return BadRequest();
        }

        if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
        {
            return Ok();
        }

        var paymentRecord = await dbContext.PaymentRecords.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);
        if (paymentRecord is not null)
        {
            paymentRecord.Status = paymentIntent.Status;
        }

        var seatPurchase = await dbContext.SeatPurchases.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripePaymentIntentId == paymentIntent.Id);
        if (seatPurchase is not null)
        {
            seatPurchase.Status = paymentIntent.Status;
            if (string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase) && !seatPurchase.SeatsApplied)
            {
                var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == seatPurchase.TenantId);
                if (tenant is not null)
                {
                    tenant.SeatAllowance += seatPurchase.SeatsAdded;
                    tenant.Tier = TenantTier.Pro;
                    seatPurchase.SeatsApplied = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
        return Ok();
    }
}
