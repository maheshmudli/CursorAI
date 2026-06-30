using System;

namespace ShiftManager.Models
{
    public enum ProvisioningStatus { Pending, Completed, Failed }

    public class ProvisioningRecord
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;
        public string Route { get; set; } = string.Empty;
        public ProvisioningStatus Status { get; set; } = ProvisioningStatus.Pending;
    }
}
