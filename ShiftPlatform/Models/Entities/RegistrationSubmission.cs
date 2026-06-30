using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models.Entities;

public class RegistrationSubmission
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
  public string AdditionalUsersJson { get; set; } = "[]";
    public RegistrationStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? TenantId { get; set; }
    public string? StripePaymentIntentId { get; set; }

    public Tenant? Tenant { get; set; }
}
