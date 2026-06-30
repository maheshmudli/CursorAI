using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShiftPlatform.Services;
using Stripe;

namespace ShiftPlatform.Controllers.Api;

[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly StripeSettings _settings;
    private readonly ISeatService _seatService;

    public StripeWebhookController(IOptions<StripeSettings> settings, ISeatService seatService)
    {
        _settings = settings.Value;
        _seatService = seatService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _settings.WebhookSecret);
        }
        catch
        {
            return BadRequest();
        }

        if (stripeEvent.Type == "payment_intent.succeeded" ||
            stripeEvent.Type == "payment_intent.payment_failed" ||
            stripeEvent.Type == "payment_intent.canceled")
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            if (intent != null)
            {
                await _seatService.ApplyWebhookPaymentAsync(intent.Id, intent.Status);
            }
        }

        return Ok();
    }
}
