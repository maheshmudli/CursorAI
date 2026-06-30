using System;

namespace ShiftManager.Models
{
    public enum PaymentStatus { Pending, Succeeded, Failed, Cancelled }

    public class PaymentRecord
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = "aud";
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Purpose { get; set; } = "registration";
    }
}
