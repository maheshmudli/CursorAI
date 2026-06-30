using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftManager.Data;
using ShiftManager.Models;
using Stripe;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Api
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public StripeWebhookController(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret))
            {
                return BadRequest("Webhook secret not configured");
            }

            string json;
            using (var reader = new StreamReader(HttpContext.Request.Body))
            {
                json = await reader.ReadToEndAsync();
            }

            Event? stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret);
            }
            catch (StripeException ex)
            {
                return BadRequest($"Webhook verification failed: {ex.Message}");
            }

            if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
            {
                await HandlePaymentIntentEventAsync(stripeEvent.Type, paymentIntent);
            }

            return Ok();
        }

        private async Task HandlePaymentIntentEventAsync(string eventType, PaymentIntent paymentIntent)
        {
            var status = eventType switch
            {
                "payment_intent.succeeded" => PaymentStatus.Succeeded,
                "payment_intent.payment_failed" => PaymentStatus.Failed,
                "payment_intent.canceled" => PaymentStatus.Cancelled,
                _ => (PaymentStatus?)null
            };

            if (status == null) return;

            // Update PaymentRecord
            var paymentRecord = await _db.PaymentRecords.IgnoreQueryFilters()
                .FirstOrDefaultAsync(pr => pr.StripePaymentIntentId == paymentIntent.Id);
            if (paymentRecord != null)
            {
                paymentRecord.Status = status.Value;
                await _db.SaveChangesAsync();
            }

            // Update SeatPurchase
            var seatPurchase = await _db.SeatPurchases.IgnoreQueryFilters()
                .FirstOrDefaultAsync(sp => sp.StripePaymentIntentId == paymentIntent.Id);
            if (seatPurchase != null)
            {
                seatPurchase.Status = status.Value;
                await _db.SaveChangesAsync();
            }

            // Update RegistrationAttempt
            var regAttempt = await _db.RegistrationAttempts
                .FirstOrDefaultAsync(r => r.StripePaymentIntentId == paymentIntent.Id);
            if (regAttempt != null && regAttempt.Status == RegistrationStatus.PaymentPending)
            {
                if (status == PaymentStatus.Failed || status == PaymentStatus.Cancelled)
                {
                    regAttempt.Status = RegistrationStatus.PaymentFailed;
                    regAttempt.FailureReason = $"Webhook: {eventType}";
                    await _db.SaveChangesAsync();
                }
            }
        }
    }
}
