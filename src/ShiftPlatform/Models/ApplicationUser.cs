using Microsoft.AspNetCore.Identity;

namespace ShiftPlatform.Models;

/// <summary>
/// Identity user extended with the owning tenant and a display name. Platform
/// Owners are not bound to a tenant, so <see cref="TenantId"/> is nullable.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string FullName { get; set; } = string.Empty;
}
