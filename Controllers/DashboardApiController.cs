using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.CompanyAdmin},{Roles.User}")]
[Route("t/{tenantSlug}/api/dashboard")]
public sealed class DashboardApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public DashboardApiController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var windowEnd = today.AddDays(28);
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole(Roles.CompanyAdmin);

        var assignmentsQuery = _db.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.User)
            .Where(a => a.Shift.Date >= today && a.Shift.Date <= windowEnd);

        if (!isAdmin)
        {
            assignmentsQuery = assignmentsQuery.Where(a => a.UserId == currentUserId);
        }

        var assignments = await assignmentsQuery
            .OrderBy(a => a.Shift.Date)
            .ThenBy(a => a.Shift.StartTime)
            .Select(a => new
            {
                AssignmentId = a.Id,
                a.ShiftId,
                a.UserId,
                UserName = a.User.FullName,
                a.Shift.Date,
                a.Shift.StartTime,
                a.Shift.EndTime,
                a.Shift.RoleLabel,
                a.Shift.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            UpcomingWindowDays = 28,
            From = today,
            To = windowEnd,
            Assignments = assignments
        });
    }

    private bool TenantAllowed() =>
        _tenantProvider.TenantId is not null &&
        User.FindFirst("tenant_id")?.Value == _tenantProvider.TenantId.Value.ToString();
}
