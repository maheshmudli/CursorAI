using System;

namespace ShiftManager.Models
{
    public class ShiftAssignment
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
