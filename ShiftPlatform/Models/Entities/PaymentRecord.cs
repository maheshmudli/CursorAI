using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models.Entities;

public class PaymentRecord
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
