using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.Models;

public enum TenantTier
{
    Base = 0,
    Pro = 1
}

public enum TenantStatus
{
    PendingPayment = 0,
    Active = 1,
    Suspended = 2
}

/// <summary>
/// A provisioned company workspace. Every tenant-owned record carries a
/// <c>TenantId</c> foreign key pointing back here, and all tenant queries are
/// filtered by the current tenant using an EF Core global query filter.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>URL slug used for path-based tenant routing (/t/{slug}).</summary>
    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public TenantTier Tier { get; set; } = TenantTier.Base;

    /// <summary>
    /// Number of paid seats. Five are included on the base plan; each extra
    /// seat is bought for 5.00 AUD and bumps the company onto the Pro tier.
    /// </summary>
    public int SeatAllowance { get; set; } = PlatformConstants.IncludedSeats;
}
