using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers;

public class HomeController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    ITenantContext tenantContext) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole(ApplicationRoles.PlatformOwner))
            {
                return RedirectToAction("Dashboard", "Platform");
            }

            if (tenantContext.CurrentTenantSlug is not null)
            {
                return RedirectToAction("Index", "Workspace", new { tenantSlug = tenantContext.CurrentTenantSlug });
            }

            var user = await userManager.GetUserAsync(User);
            if (user?.TenantId is not null)
            {
                var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId);
                if (tenant is not null)
                {
                    return Redirect($"/t/{tenant.Slug}");
                }
            }
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
