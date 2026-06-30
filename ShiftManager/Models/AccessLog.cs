using System;

namespace ShiftManager.Models
{
    public class AccessLog
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    }
}
