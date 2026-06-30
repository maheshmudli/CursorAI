using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Controllers;

public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

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

        var user = await _userManager.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var accessLog = await _db.AccessLogs.IgnoreQueryFilters().SingleOrDefaultAsync(l => l.UserId == user.Id);
        if (accessLog is null)
        {
            _db.AccessLogs.Add(new AccessLog { UserId = user.Id, TenantId = user.TenantId });
        }
        else
        {
            accessLog.LastLoginAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();

        if (await _userManager.IsInRoleAsync(user, Roles.PlatformOwner))
        {
            return RedirectToAction("Index", "Platform");
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == user.TenantId);
        return RedirectToAction("Dashboard", "Tenant", new { tenantSlug = tenant.Slug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
