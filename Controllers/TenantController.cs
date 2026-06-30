using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Controllers;

[Authorize(Roles = $"{Roles.CompanyAdmin},{Roles.User}")]
public sealed class TenantController : Controller
{
    private readonly ITenantProvider _tenantProvider;

    public TenantController(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public IActionResult Dashboard()
    {
        if (_tenantProvider.TenantId is null)
        {
            return NotFound();
        }

        return View();
    }

    public IActionResult Shifts()
    {
        if (_tenantProvider.TenantId is null)
        {
            return NotFound();
        }

        return View();
    }

    [Authorize(Roles = Roles.CompanyAdmin)]
    public IActionResult Users()
    {
        if (_tenantProvider.TenantId is null)
        {
            return NotFound();
        }

        return View();
    }
}
