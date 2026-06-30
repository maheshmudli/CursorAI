using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var users = await _db.Users
            .Where(u => u.TenantId == _tenantContext.TenantId)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? Constants.UserRole
            });
        }

        return Ok(result);
    }

    private async Task<bool> AuthorizeTenantUser()
    {
        if (!_tenantContext.HasTenant) return false;
        if (User.IsInRole(Constants.PlatformOwnerRole)) return false;
        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }
}
