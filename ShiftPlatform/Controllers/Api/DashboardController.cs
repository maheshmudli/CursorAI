using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

[ApiController]
[Authorize(Roles = $"{ApplicationRoles.CompanyAdmin},{ApplicationRoles.User}")]
[Route("t/{tenantSlug}/api/dashboard")]
public class DashboardController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITenantContext tenantContext) : ControllerBase
{
    private const int UpcomingWindowDays = 28;

    [HttpGet]
    public async Task<IActionResult> Get(string tenantSlug)
    {
        if (!tenantContext.IsTenantRoute || !string.Equals(tenantContext.CurrentTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = now.AddDays(UpcomingWindowDays);
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var assignmentsQuery = dbContext.ShiftAssignments
            .AsNoTracking()
            .Include(a => a.Shift)
            .Include(a => a.User)
            .Where(a => a.Shift!.Date >= now && a.Shift.Date <= toDate);

        if (!User.IsInRole(ApplicationRoles.CompanyAdmin))
        {
            assignmentsQuery = assignmentsQuery.Where(a => a.UserId == currentUser.Id);
        }

        var assignments = await assignmentsQuery
            .OrderBy(a => a.Shift!.Date)
            .ThenBy(a => a.Shift!.StartTime)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                UserName = a.User!.FullName,
                ShiftId = a.ShiftId,
                a.Shift!.Date,
                a.Shift.StartTime,
                a.Shift.EndTime,
                a.Shift.RoleLabel
            })
            .ToListAsync();

        return Ok(new
        {
            upcomingWindowDays = UpcomingWindowDays,
            startDate = now,
            endDate = toDate,
            assignments
        });
    }
}
