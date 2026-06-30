namespace ShiftPlatform.Models;

/// <summary>Audit record written when a tenant workspace is provisioned.</summary>
public class ProvisioningRecord
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The route the workspace is reachable at, e.g. <c>/t/acme</c>.</summary>
    public string Route { get; set; } = string.Empty;

    public string Status { get; set; } = "Provisioned";
}
