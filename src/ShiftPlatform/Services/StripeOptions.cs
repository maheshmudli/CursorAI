namespace ShiftPlatform.Services;

/// <summary>Stripe configuration, bound from the "Stripe" config section / user secrets.</summary>
public class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>True when a real Stripe secret key has been configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && SecretKey.StartsWith("sk_");
}
