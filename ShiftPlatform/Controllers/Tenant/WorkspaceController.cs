using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.ViewModels;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Tenant;

[Authorize]
[Route($"{Constants.TenantRoutePrefix}/{{tenantSlug}}")]
public class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantContext _tenantContext;

    public WorkspaceController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantContext tenantContext)
    {
        _db = db;
        _userManager = userManager;
        _tenantContext = tenantContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string tenantSlug)
    {
        if (!await ValidateTenantAccess(tenantSlug))
        {
            return Forbid();
        }

        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Slug == tenantSlug);
        ViewBag.TenantSlug = tenantSlug;
        ViewBag.CompanyName = tenant.CompanyName;
        ViewBag.IsAdmin = User.IsInRole(Constants.CompanyAdminRole);
        return View();
    }

    [HttpGet("shifts")]
    [Authorize(Roles = $"{Constants.CompanyAdminRole}")]
    public async Task<IActionResult> Shifts(string tenantSlug)
    {
        if (!await ValidateTenantAccess(tenantSlug)) return Forbid();
        ViewBag.TenantSlug = tenantSlug;
        return View();
    }

    [HttpGet("swaps")]
    public async Task<IActionResult> Swaps(string tenantSlug)
    {
        if (!await ValidateTenantAccess(tenantSlug)) return Forbid();
        ViewBag.TenantSlug = tenantSlug;
        ViewBag.IsAdmin = User.IsInRole(Constants.CompanyAdminRole);
        return View();
    }

    private async Task<bool> ValidateTenantAccess(string tenantSlug)
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantSlug != tenantSlug)
        {
            return false;
        }

        if (User.IsInRole(Constants.PlatformOwnerRole))
        {
            return true;
        }

        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }
}
