namespace ShiftPlatform.Models;

public enum SwapStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// A request from one user to swap one of their shift assignments with another
/// user's assignment. Approval exchanges the two assignments' owners.
/// </summary>
public class SwapRequest
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string RequestingUserId { get; set; } = string.Empty;
    public ApplicationUser? RequestingUser { get; set; }

    public int RequestingAssignmentId { get; set; }
    public ShiftAssignment? RequestingAssignment { get; set; }

    public string TargetUserId { get; set; } = string.Empty;
    public ApplicationUser? TargetUser { get; set; }

    public int TargetAssignmentId { get; set; }
    public ShiftAssignment? TargetAssignment { get; set; }

    public SwapStatus Status { get; set; } = SwapStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
