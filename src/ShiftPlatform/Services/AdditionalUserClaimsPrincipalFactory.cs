using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ShiftPlatform.Models;

namespace ShiftPlatform.Services;

/// <summary>
/// Adds the user's TenantId and full name as claims so the tenant-resolution
/// middleware can cheaply enforce that a user only accesses their own tenant.
/// </summary>
public class AdditionalUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string TenantIdClaim = "TenantId";

    public AdditionalUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.TenantId.HasValue)
            identity.AddClaim(new Claim(TenantIdClaim, user.TenantId.Value.ToString()));
        identity.AddClaim(new Claim("FullName", user.FullName ?? string.Empty));
        return identity;
    }
}
