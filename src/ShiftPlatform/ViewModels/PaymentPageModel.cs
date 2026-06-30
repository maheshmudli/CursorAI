namespace ShiftPlatform.ViewModels;

/// <summary>Drives the shared Stripe Elements payment page (registration & seats).</summary>
public class PaymentPageModel
{
    public Guid Token { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "aud";
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public bool IsLiveStripe { get; set; }

    /// <summary>"registration" or "seats" — selects the post-payment action.</summary>
    public string Purpose { get; set; } = "registration";

    /// <summary>For seat purchases: the workspace slug and seat count.</summary>
    public string? Slug { get; set; }
    public int Seats { get; set; }

    public string AmountDisplay => $"{AmountCents / 100m:0.00} {Currency.ToUpperInvariant()}";
}
