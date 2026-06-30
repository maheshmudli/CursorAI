using Microsoft.Extensions.Options;
using Stripe;

namespace ShiftPlatform.Services;

/// <summary>Real Stripe implementation using Payment Intents via the Stripe .NET SDK.</summary>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        StripeConfiguration.ApiKey = _options.SecretKey;
    }

    public bool IsLive => true;
    public string PublishableKey => _options.PublishableKey;

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(long amountCents, string currency, IDictionary<string, string> metadata)
    {
        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency,
            // Captured by default; see README for how to switch to manual
            // capture (auth + release) or an immediate refund instead.
            CaptureMethod = "automatic",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = new Dictionary<string, string>(metadata)
        });

        return new PaymentIntentResult(intent.Id, intent.ClientSecret, intent.Status);
    }

    public async Task<PaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        var intent = await service.GetAsync(paymentIntentId);
        return new PaymentIntentResult(intent.Id, intent.ClientSecret, intent.Status);
    }

    public WebhookResult ParseWebhook(string json, string? stripeSignatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = string.IsNullOrEmpty(_options.WebhookSecret)
                ? EventUtility.ParseEvent(json)
                : EventUtility.ConstructEvent(json, stripeSignatureHeader, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to verify Stripe webhook signature.");
            return new WebhookResult(false, string.Empty, string.Empty, string.Empty);
        }

        if (stripeEvent.Data.Object is PaymentIntent pi)
        {
            return new WebhookResult(true, stripeEvent.Type, pi.Id, pi.Status);
        }

        return new WebhookResult(false, stripeEvent.Type, string.Empty, string.Empty);
    }

    public Task<PaymentIntentResult> ConfirmTestPaymentAsync(string paymentIntentId, bool success)
        => throw new InvalidOperationException("Test confirmation is not available against live Stripe; the browser confirms the Payment Intent with Stripe.js.");
}
