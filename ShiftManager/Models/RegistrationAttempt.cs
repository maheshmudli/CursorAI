using System;

namespace ShiftManager.Models
{
    public enum RegistrationStatus { Started, PaymentPending, PaymentFailed, Completed, Abandoned }

    public class RegistrationAttempt
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Started;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? StripePaymentIntentId { get; set; }
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public string? FailureReason { get; set; }
    }
}
