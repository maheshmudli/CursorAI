using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.ViewModels;
using System;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Platform
{
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
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Update last login
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Log access
                    if (user.TenantId.HasValue)
                    {
                        var existingLog = await _db.AccessLogs.IgnoreQueryFilters()
                            .FirstOrDefaultAsync(al => al.UserId == user.Id && al.TenantId == user.TenantId);
                        if (existingLog != null)
                        {
                            existingLog.LastLoginAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _db.AccessLogs.Add(new AccessLog
                            {
                                TenantId = user.TenantId.Value,
                                UserId = user.Id,
                                LastLoginAt = DateTime.UtcNow
                            });
                        }
                        await _db.SaveChangesAsync();
                    }

                    // Route based on role
                    if (await _userManager.IsInRoleAsync(user, "PlatformOwner"))
                    {
                        return RedirectToAction("Index", "PlatformDashboard");
                    }
                    else if (user.TenantId.HasValue)
                    {
                        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);
                        if (tenant != null)
                        {
                            return Redirect($"/t/{tenant.Slug}");
                        }
                    }
                }
                return RedirectToLocal(model.ReturnUrl);
            }
            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked. Please try again later.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
