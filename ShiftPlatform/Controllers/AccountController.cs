using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

[AllowAnonymous]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    ITenantContext tenantContext) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var existingLog = await dbContext.AccessLogs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.TenantId == (user.TenantId ?? Guid.Empty));
        if (existingLog is null && user.TenantId.HasValue)
        {
            dbContext.AccessLogs.Add(new AccessLog
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId.Value,
                UserId = user.Id,
                LastLoginAt = DateTime.UtcNow
            });
        }
        else if (existingLog is not null)
        {
            existingLog.LastLoginAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();

        if (await userManager.IsInRoleAsync(user, ApplicationRoles.PlatformOwner))
        {
            return RedirectToAction("Dashboard", "Platform");
        }

        if (user.TenantId.HasValue)
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == user.TenantId.Value);
            return RedirectToAction("Index", "Workspace", new { tenantSlug = tenant.Slug });
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        tenantContext.Clear();
        return RedirectToAction("Index", "Home");
    }
}
