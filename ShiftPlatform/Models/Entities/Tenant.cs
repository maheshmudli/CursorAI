using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public TenantTier Tier { get; set; } = TenantTier.Base;
    public int SeatAllowance { get; set; } = Infrastructure.Constants.IncludedSeats;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<ProvisioningRecord> ProvisioningRecords { get; set; } = new List<ProvisioningRecord>();
    public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
    public ICollection<SeatPurchase> SeatPurchases { get; set; } = new List<SeatPurchase>();
}
