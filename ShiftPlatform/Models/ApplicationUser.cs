using Microsoft.AspNetCore.Identity;

namespace ShiftPlatform.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public bool IsStaff { get; set; } = true;
    public bool MustChangePassword { get; set; }

    public Tenant? Tenant { get; set; }
}
