using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

/// <summary>
/// Base for all tenant-scoped JSON API controllers. Routes live under
/// /t/{slug}/api/... so the tenant-resolution middleware sets the tenant context
/// (powering the EF global query filter) and rejects cross-tenant access before
/// any action runs.
/// </summary>
[ApiController]
[Authorize]
[Route("t/{slug}/api")]
[Produces("application/json")]
public abstract class TenantApiController : ControllerBase
{
    protected readonly ITenantContext Tenant;

    protected TenantApiController(ITenantContext tenant) => Tenant = tenant;

    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    protected bool IsAdmin => User.IsInRole(PlatformConstants.Roles.CompanyAdmin);
    protected int TenantId => Tenant.TenantId ?? 0;
}
