using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftPlatform.Constants;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers;

[Authorize(Roles = $"{ApplicationRoles.CompanyAdmin},{ApplicationRoles.User}")]
[Route("t/{tenantSlug}")]
public class WorkspaceController(ITenantContext tenantContext) : Controller
{
    [HttpGet("")]
    public IActionResult Index(string tenantSlug)
    {
        if (!tenantContext.IsTenantRoute || !string.Equals(tenantContext.CurrentTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        ViewBag.TenantSlug = tenantSlug;
        return View();
    }

    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    [HttpGet("users")]
    public IActionResult Users(string tenantSlug)
    {
        ViewBag.TenantSlug = tenantSlug;
        return View();
    }
}
