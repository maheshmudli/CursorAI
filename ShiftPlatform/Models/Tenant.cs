using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active";
    public TenantTier Tier { get; set; } = TenantTier.Base;
    public int SeatAllowance { get; set; } = 5;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
