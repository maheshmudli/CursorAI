using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using Stripe;

namespace ShiftManagementPlatform.Controllers;

[Route("stripe/webhook")]
public sealed class StripeWebhookController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public StripeWebhookController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Handle()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"];
        var secret = _configuration["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = string.IsNullOrWhiteSpace(secret)
                ? EventUtility.ParseEvent(json)
                : EventUtility.ConstructEvent(json, signature, secret);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        if (stripeEvent.Data.Object is PaymentIntent intent)
        {
            var payment = await _db.PaymentRecords.IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.StripePaymentIntentId == intent.Id);
            if (payment is not null)
            {
                payment.Status = intent.Status;
            }

            var seat = await _db.SeatPurchases.IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.StripePaymentIntentId == intent.Id);
            if (seat is not null)
            {
                seat.Status = intent.Status;
            }

            await _db.SaveChangesAsync();
        }

        return Ok();
    }
}
