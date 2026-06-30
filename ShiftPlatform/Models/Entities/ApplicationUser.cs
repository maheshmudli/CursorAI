using Microsoft.AspNetCore.Identity;

namespace ShiftPlatform.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public int? TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? TemporaryPassword { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
}
