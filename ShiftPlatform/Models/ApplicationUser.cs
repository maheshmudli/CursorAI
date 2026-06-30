using Microsoft.AspNetCore.Identity;

namespace ShiftPlatform.Models;

public class ApplicationUser : IdentityUser
{
    public Guid? TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}
