namespace ShiftPlatform.Models;

/// <summary>
/// A one-off purchase of one or more extra seats (5.00 AUD each) under the Pro
/// license. Recorded per Stripe Payment Intent and visible to the Platform Owner.
/// </summary>
public class SeatPurchase
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string StripePaymentIntentId { get; set; } = string.Empty;

    /// <summary>Total amount in cents (5.00 AUD * SeatsAdded).</summary>
    public long Amount { get; set; }

    public string Currency { get; set; } = PlatformConstants.Currency;

    public string Status { get; set; } = string.Empty;

    public int SeatsAdded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set true once the paid seats have been applied to the tenant's allowance,
    /// so a replayed webhook can't grant the seats twice.
    /// </summary>
    public bool Applied { get; set; }
}
