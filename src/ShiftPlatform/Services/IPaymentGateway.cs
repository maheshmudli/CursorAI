namespace ShiftPlatform.Services;

public record PaymentIntentResult(string Id, string? ClientSecret, string Status);

public record WebhookResult(bool Handled, string EventType, string PaymentIntentId, string Status);

/// <summary>
/// Abstraction over Stripe Payment Intents. The real implementation uses the
/// official Stripe .NET SDK; a development fake stands in when no Stripe secret
/// key is configured so the full flow can be exercised locally.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>True when backed by real Stripe; false for the dev fake.</summary>
    bool IsLive { get; }

    string PublishableKey { get; }

    Task<PaymentIntentResult> CreatePaymentIntentAsync(long amountCents, string currency, IDictionary<string, string> metadata);

    Task<PaymentIntentResult> GetPaymentIntentAsync(string paymentIntentId);

    /// <summary>
    /// Verify (and parse) an incoming webhook payload. Returns the resolved
    /// payment intent id and status when the event is a payment_intent event.
    /// </summary>
    WebhookResult ParseWebhook(string json, string? stripeSignatureHeader);

    /// <summary>
    /// Dev-only: mark a fake payment intent as succeeded or failed. Throws for
    /// the real Stripe gateway, where the browser confirms the intent instead.
    /// </summary>
    Task<PaymentIntentResult> ConfirmTestPaymentAsync(string paymentIntentId, bool success);
}
