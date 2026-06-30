using Microsoft.AspNetCore.Identity;
using System;

namespace ShiftManager.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string? TemporaryPassword { get; set; }
        public bool MustChangePassword { get; set; } = false;
    }
}
