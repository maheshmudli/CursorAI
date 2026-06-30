using System;
using System.Collections.Generic;

namespace ShiftManager.Models
{
    public enum TenantStatus { Active, Suspended, Cancelled }
    public enum TenantTier { Base, Pro }

    public class Tenant
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TenantStatus Status { get; set; } = TenantStatus.Active;
        public TenantTier Tier { get; set; } = TenantTier.Base;
        public int SeatAllowance { get; set; } = 5;

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
        public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
        public ICollection<SwapRequest> SwapRequests { get; set; } = new List<SwapRequest>();
        public ProvisioningRecord? ProvisioningRecord { get; set; }
        public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
        public ICollection<SeatPurchase> SeatPurchases { get; set; } = new List<SeatPurchase>();
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
    }
}
