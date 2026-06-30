using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ShiftManagementPlatform.Models;

public static class Roles
{
    public const string PlatformOwner = "Platform Owner";
    public const string CompanyAdmin = "Company Admin";
    public const string User = "User";
}

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended
}

public enum TenantTier
{
    Base,
    Pro
}

public enum RegistrationStatus
{
    Submitted,
    PaymentFailed,
    Provisioned
}

public enum PaymentPurpose
{
    RegistrationValidation,
    SeatPurchase
}

public enum SwapStatus
{
    Pending,
    Approved,
    Rejected
}

public interface ITenantOwned
{
    int TenantId { get; set; }
}

public sealed class Tenant
{
    public int Id { get; set; }
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
    public TenantTier Tier { get; set; } = TenantTier.Base;
    public int SeatAllowance { get; set; } = 5;
}

public sealed class ApplicationUser : IdentityUser
{
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
}

public sealed class RegistrationSubmission
{
    public int Id { get; set; }
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string AdminName { get; set; } = string.Empty;
    [MaxLength(256)]
    public string AdminEmail { get; set; } = string.Empty;
    public int RequestedUserCount { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Submitted;
    public string? FailureReason { get; set; }
    public int? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Shift : ITenantOwned
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    [MaxLength(120)]
    public string RoleLabel { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
}

public sealed class ShiftAssignment : ITenantOwned
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SwapRequest : ITenantOwned
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string RequestingUserId { get; set; } = string.Empty;
    public int RequestingAssignmentId { get; set; }
    public ShiftAssignment RequestingAssignment { get; set; } = null!;
    public string TargetUserId { get; set; } = string.Empty;
    public int TargetAssignmentId { get; set; }
    public ShiftAssignment TargetAssignment { get; set; } = null!;
    public SwapStatus Status { get; set; } = SwapStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class ProvisioningRecord
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateTimeOffset ProvisionedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(300)]
    public string Route { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Status { get; set; } = "Provisioned";
}

public sealed class PaymentRecord
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    [MaxLength(200)]
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    [MaxLength(3)]
    public string Currency { get; set; } = "aud";
    [MaxLength(80)]
    public string Status { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SeatPurchase
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    [MaxLength(200)]
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    [MaxLength(3)]
    public string Currency { get; set; } = "aud";
    [MaxLength(80)]
    public string Status { get; set; } = string.Empty;
    public int SeatsAdded { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AccessLog
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;
}
