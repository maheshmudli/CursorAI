namespace ShiftPlatform.Options;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public bool EnablePayments { get; set; } = false;
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}
