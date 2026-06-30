using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Models.Entities;

public class ProvisioningRecord
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime ProvisionedAt { get; set; }
    public string Route { get; set; } = string.Empty;
    public ProvisioningStatus Status { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
