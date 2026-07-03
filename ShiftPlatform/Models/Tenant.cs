using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TenantStatus Status { get; set; } = TenantStatus.Pending;
    public TenantTier Tier { get; set; } = TenantTier.Base;
    public int SeatAllowance { get; set; } = 5;
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Aptos";
    public string LogoPath { get; set; } = string.Empty;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
