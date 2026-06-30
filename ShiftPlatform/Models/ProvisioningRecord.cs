namespace ShiftPlatform.Models;

public class ProvisioningRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;
    public string Route { get; set; } = string.Empty;
    public string Status { get; set; } = "Provisioned";

    public Tenant? Tenant { get; set; }
}
