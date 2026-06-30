using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

/// <summary>
/// Single login form that authenticates the user, records access, and routes
/// them to the right place: Platform Owners to the tracking dashboard, tenant
/// users into their workspace.
/// </summary>
public class AccountController : Controller
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
    [AllowAnonymous]
    public IActionResult Index() => RedirectToDestination(User.Identity?.IsAuthenticated == true ? User : null);

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToDestinationForCurrentUser();
        return View(new LoginInput());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);

        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(input);
        }

        var result = await _signInManager.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(input);
        }

        await RecordAccessAsync(user);
        return await RedirectToDestinationAsync(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task RecordAccessAsync(ApplicationUser user)
    {
        if (user.TenantId == null) return;
        var log = await _db.AccessLogs.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (log == null)
            _db.AccessLogs.Add(new AccessLog { TenantId = user.TenantId.Value, UserId = user.Id, LastLoginAt = DateTime.UtcNow });
        else
            log.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<IActionResult> RedirectToDestinationAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, PlatformConstants.Roles.PlatformOwner))
            return RedirectToAction("Index", "Platform");

        if (user.TenantId != null)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
            if (tenant != null)
                return Redirect($"/t/{tenant.Slug}/workspace");
        }
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToDestinationForCurrentUser()
    {
        if (User.IsInRole(PlatformConstants.Roles.PlatformOwner))
            return RedirectToAction("Index", "Platform");
        return RedirectToAction("Index", "Home");
    }

    private IActionResult RedirectToDestination(System.Security.Claims.ClaimsPrincipal? principal)
    {
        if (principal == null) return RedirectToAction(nameof(Login));
        return RedirectToDestinationForCurrentUser();
    }
}
