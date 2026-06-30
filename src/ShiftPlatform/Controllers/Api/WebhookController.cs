using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

/// <summary>
/// Stripe webhook endpoint. The final payment status is confirmed here rather
/// than trusting the browser alone: succeeded validation payments and seat
/// purchases are reconciled against their records.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("webhook/stripe")]
public class WebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ISeatService _seatService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(ApplicationDbContext db, IPaymentGateway gateway, ISeatService seatService, ILogger<WebhookController> logger)
    {
        _db = db;
        _gateway = gateway;
        _seatService = seatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        var result = _gateway.ParseWebhook(json, signature);
        if (!result.Handled || string.IsNullOrEmpty(result.PaymentIntentId))
            return BadRequest();

        _logger.LogInformation("Stripe webhook {Type} for {IntentId} status {Status}.",
            result.EventType, result.PaymentIntentId, result.Status);

        // Seat purchase reconciliation (also bumps the allowance when succeeded).
        var seat = await _db.SeatPurchases.FirstOrDefaultAsync(s => s.StripePaymentIntentId == result.PaymentIntentId);
        if (seat != null)
        {
            await _seatService.ApplySeatPurchaseAsync(result.PaymentIntentId, result.Status);
            return Ok();
        }

        // Validation payment for a provisioned tenant.
        var payment = await _db.PaymentRecords.FirstOrDefaultAsync(p => p.StripePaymentIntentId == result.PaymentIntentId);
        if (payment != null)
        {
            payment.Status = result.Status;
            await _db.SaveChangesAsync();
            return Ok();
        }

        // Registration still pending: record the confirmed status.
        var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.StripePaymentIntentId == result.PaymentIntentId);
        if (pending != null && pending.Status is "Submitted" or "Failed")
        {
            pending.Status = result.Status == "succeeded" ? "Paid" : pending.Status;
            await _db.SaveChangesAsync();
        }

        return Ok();
    }
}
