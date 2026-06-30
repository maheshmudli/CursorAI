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
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var user = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole(Constants.CompanyAdminRole);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingEnd = today.AddDays(Constants.UpcomingWeeks * 7);

        var shiftsQuery = _db.Shifts
            .Include(s => s.Assignment)
            .ThenInclude(a => a!.User)
            .Where(s => s.Date >= today && s.Date <= upcomingEnd)
            .AsQueryable();

        if (!isAdmin)
        {
            shiftsQuery = shiftsQuery.Where(s => s.Assignment != null && s.Assignment.UserId == user!.Id);
        }

        var upcoming = await shiftsQuery
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        var assigned = upcoming
            .Where(s => s.Assignment != null && (isAdmin || s.Assignment!.UserId == user!.Id))
            .Select(MapShift)
            .ToList();

        return Ok(new DashboardDto
        {
            AssignedShifts = assigned,
            UpcomingShifts = upcoming.Select(MapShift).ToList(),
            UpcomingWeeks = Constants.UpcomingWeeks
        });
    }

    private async Task<bool> AuthorizeTenantUser()
    {
        if (!_tenantContext.HasTenant) return false;
        if (User.IsInRole(Constants.PlatformOwnerRole)) return false;
        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }

    private static ShiftDto MapShift(Shift shift) => new()
    {
        Id = shift.Id,
        Date = shift.Date.ToString("yyyy-MM-dd"),
        StartTime = shift.StartTime.ToString("HH:mm"),
        EndTime = shift.EndTime.ToString("HH:mm"),
        RoleLabel = shift.RoleLabel,
        Notes = shift.Notes,
        AssignedUserId = shift.Assignment?.UserId,
        AssignedUserName = shift.Assignment?.User?.FullName,
        AssignmentId = shift.Assignment?.Id
    };
}
