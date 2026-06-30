using ShiftPlatform.Models.Enums;
using ShiftPlatform.Models.Interfaces;

namespace ShiftPlatform.Models;

public class SwapRequest : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RequestingUserId { get; set; } = string.Empty;
    public Guid RequestingAssignmentId { get; set; }
    public string TargetUserId { get; set; } = string.Empty;
    public Guid TargetAssignmentId { get; set; }
    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public ApplicationUser? RequestingUser { get; set; }
    public ShiftAssignment? RequestingAssignment { get; set; }
    public ApplicationUser? TargetUser { get; set; }
    public ShiftAssignment? TargetAssignment { get; set; }
}
