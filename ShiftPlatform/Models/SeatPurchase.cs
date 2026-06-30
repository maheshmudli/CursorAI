namespace ShiftPlatform.Models;

public class SeatPurchase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "aud";
    public string Status { get; set; } = "pending";
    public int SeatsAdded { get; set; }
    public bool SeatsApplied { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}
