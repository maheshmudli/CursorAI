namespace ShiftPlatform.Models;

/// <summary>
/// The 1.00 AUD card-validation payment taken at registration, tracked against
/// the tenant it provisioned.
/// </summary>
public class PaymentRecord
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string StripePaymentIntentId { get; set; } = string.Empty;

    /// <summary>Amount in the currency's minor unit (cents).</summary>
    public long Amount { get; set; }

    public string Currency { get; set; } = PlatformConstants.Currency;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
