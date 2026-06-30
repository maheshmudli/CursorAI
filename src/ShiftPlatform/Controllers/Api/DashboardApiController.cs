using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Route("t/{slug}/api/dashboard")]
public class DashboardApiController : TenantApiController
{
    private readonly ApplicationDbContext _db;

    public DashboardApiController(ITenantContext tenant, ApplicationDbContext db) : base(tenant) => _db = db;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowEnd = today.AddDays(PlatformConstants.UpcomingWindowDays);

        var myShifts = await _db.Shifts
            .Include(s => s.Assignment).ThenInclude(a => a!.User)
            .Where(s => s.Assignment != null && s.Assignment.UserId == CurrentUserId && s.Date >= today)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();

        // Admins get an overview of every assignment in the window; regular
        // users get their own upcoming shifts.
        var upcomingQuery = _db.Shifts
            .Include(s => s.Assignment).ThenInclude(a => a!.User)
            .Where(s => s.Date >= today && s.Date <= windowEnd);
        if (!IsAdmin)
            upcomingQuery = upcomingQuery.Where(s => s.Assignment != null && s.Assignment.UserId == CurrentUserId);

        var upcoming = await upcomingQuery.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();

        return Ok(new DashboardDto
        {
            IsAdmin = IsAdmin,
            UpcomingWindowDays = PlatformConstants.UpcomingWindowDays,
            MyShifts = myShifts.Select(ShiftsApiController.ToDto).ToList(),
            Upcoming = upcoming.Select(ShiftsApiController.ToDto).ToList()
        });
    }
}
