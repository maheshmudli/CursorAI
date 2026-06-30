using System.Collections.Concurrent;
using System.Text.Json;

namespace ShiftPlatform.Services;

/// <summary>
/// Development stand-in for Stripe used only when no Stripe secret key is
/// configured. It mimics Payment Intents (create / confirm / retrieve) in
/// memory so the registration and seat-purchase flows can be exercised locally
/// without real card processing. Production uses <see cref="StripePaymentGateway"/>.
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    private static readonly ConcurrentDictionary<string, string> Intents = new();
    private readonly ILogger<FakePaymentGateway> _logger;

    public FakePaymentGateway(ILogger<FakePaymentGateway> logger)
    {
        _logger = logger;
        _logger.LogWarning("Stripe is not configured: using the in-memory FAKE payment gateway. Configure Stripe:SecretKey for real payments.");
    }

    public bool IsLive => false;
    public string PublishableKey => "pk_test_fake";

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(long amountCents, string currency, IDictionary<string, string> metadata)
    {
        var id = $"pi_fake_{Guid.NewGuid():N}";
        Intents[id] = "requires_payment_method";
        return Task.FromResult(new PaymentIntentResult(id, $"{id}_secret", "requires_payment_method"));
    }

    public Task<PaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId)
    {
        var status = Intents.GetValueOrDefault(paymentIntentId, "requires_payment_method");
        return Task.FromResult(new PaymentIntentResult(paymentIntentId, $"{paymentIntentId}_secret", status));
    }

    public WebhookResult ParseWebhook(string json, string? stripeSignatureHeader)
    {
        // In fake mode we accept a minimal Stripe-shaped JSON body so the webhook
        // path can be tested: { "type": "...", "data": { "object": { "id": "..", "status": ".." } } }.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var obj = root.GetProperty("data").GetProperty("object");
        var id = obj.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        var status = obj.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        if (!string.IsNullOrEmpty(id)) Intents[id] = status;
        return new WebhookResult(!string.IsNullOrEmpty(id), type, id, status);
    }

    public Task<PaymentIntentResult> ConfirmTestPaymentAsync(string paymentIntentId, bool success)
    {
        var status = success ? "succeeded" : "requires_payment_method";
        Intents[paymentIntentId] = status;
        return Task.FromResult(new PaymentIntentResult(paymentIntentId, $"{paymentIntentId}_secret", status));
    }
}
