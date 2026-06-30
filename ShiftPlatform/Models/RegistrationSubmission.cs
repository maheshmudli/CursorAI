using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models;

public class RegistrationSubmission
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public int RequestedUserCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Submitted;
    public Guid? TenantId { get; set; }
    public string? ErrorMessage { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
}
