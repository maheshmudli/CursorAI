using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models.Entities;

public class SwapRequest : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string RequestingUserId { get; set; } = string.Empty;
    public int RequestingAssignmentId { get; set; }
    public string TargetUserId { get; set; } = string.Empty;
    public int TargetAssignmentId { get; set; }
    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser RequestingUser { get; set; } = null!;
    public ApplicationUser TargetUser { get; set; } = null!;
    public ShiftAssignment RequestingAssignment { get; set; } = null!;
    public ShiftAssignment TargetAssignment { get; set; } = null!;
}
