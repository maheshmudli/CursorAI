using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Api
{
    [ApiController]
    [Authorize]
    [Route("t/{slug}/api/dashboard")]
    public class DashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly UserManager<ApplicationUser> _userManager;

        // Upcoming window: next 4 weeks
        private const int UpcomingWindowDays = 28;

        public DashboardApiController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        private async Task<bool> EnsureTenantAsync(string slug)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant == null) return false;
            _tenantContext.CurrentTenantId = tenant.Id;
            return true;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboard(string slug)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");
            var now = DateTime.UtcNow.Date;
            var windowEnd = now.AddDays(UpcomingWindowDays);

            var shiftsQuery = _db.Shifts
                .Include(s => s.Assignment).ThenInclude(a => a != null ? a.User : null)
                .Where(s => s.Date >= now && s.Date <= windowEnd);

            if (!isAdmin)
            {
                shiftsQuery = shiftsQuery.Where(s => s.Assignment != null && s.Assignment.UserId == currentUser.Id);
            }

            var shifts = (await shiftsQuery.OrderBy(s => s.Date).ToListAsync())
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            return Ok(new
            {
                IsAdmin = isAdmin,
                WindowStart = now,
                WindowEnd = windowEnd,
                MyShifts = shifts
                    .Where(s => s.Assignment?.UserId == currentUser.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.Date,
                        StartTime = s.StartTime.ToString(@"hh\:mm"),
                        EndTime = s.EndTime.ToString(@"hh\:mm"),
                        s.RoleLabel,
                        s.Notes
                    }),
                AllShifts = isAdmin ? shifts.Select(s => new
                {
                    s.Id,
                    s.Date,
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    s.RoleLabel,
                    s.Notes,
                    AssignedTo = s.Assignment?.User?.FullName
                }) : null
            });
        }
    }
}
