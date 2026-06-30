using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Api
{
    [ApiController]
    [Authorize]
    [Route("t/{slug}/api/shifts")]
    public class ShiftsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShiftsApiController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
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
        public async Task<IActionResult> GetShifts(string slug, DateTime? from = null, DateTime? to = null, string? userId = null)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");

            var query = _db.Shifts
                .Include(s => s.Assignment).ThenInclude(a => a != null ? a.User : null)
                .AsQueryable();

            if (from.HasValue) query = query.Where(s => s.Date >= from.Value);
            if (to.HasValue) query = query.Where(s => s.Date <= to.Value);

            // Non-admins can only see their own assigned shifts
            if (!isAdmin)
            {
                query = query.Where(s => s.Assignment != null && s.Assignment.UserId == currentUser.Id);
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(s => s.Assignment != null && s.Assignment.UserId == userId);
            }

            var shifts = (await query.OrderBy(s => s.Date).ToListAsync())
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            return Ok(shifts.Select(s => new
            {
                s.Id,
                s.Date,
                StartTime = s.StartTime.ToString(@"hh\:mm"),
                EndTime = s.EndTime.ToString(@"hh\:mm"),
                s.RoleLabel,
                s.Notes,
                s.CreatedAt,
                AssignedUserId = s.Assignment?.UserId,
                AssignedUserName = s.Assignment?.User?.FullName
            }));
        }

        [HttpPost]
        [Authorize(Roles = "CompanyAdmin")]
        public async Task<IActionResult> CreateShift(string slug, [FromBody] CreateShiftViewModel model)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            if (!TimeSpan.TryParseExact(model.StartTime, @"hh\:mm", null, out var start))
                return BadRequest(new { error = "Invalid start time format. Use HH:mm" });
            if (!TimeSpan.TryParseExact(model.EndTime, @"hh\:mm", null, out var end))
                return BadRequest(new { error = "Invalid end time format. Use HH:mm" });

            var shift = new Shift
            {
                TenantId = _tenantContext.CurrentTenantId!.Value,
                Date = model.Date,
                StartTime = start,
                EndTime = end,
                RoleLabel = model.RoleLabel,
                Notes = model.Notes,
                CreatedBy = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _db.Shifts.Add(shift);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetShifts), new { slug }, new { shift.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "CompanyAdmin")]
        public async Task<IActionResult> UpdateShift(string slug, int id, [FromBody] CreateShiftViewModel model)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var shift = await _db.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            if (!TimeSpan.TryParseExact(model.StartTime, @"hh\:mm", null, out var start))
                return BadRequest(new { error = "Invalid start time format" });
            if (!TimeSpan.TryParseExact(model.EndTime, @"hh\:mm", null, out var end))
                return BadRequest(new { error = "Invalid end time format" });

            shift.Date = model.Date;
            shift.StartTime = start;
            shift.EndTime = end;
            shift.RoleLabel = model.RoleLabel;
            shift.Notes = model.Notes;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "CompanyAdmin")]
        public async Task<IActionResult> DeleteShift(string slug, int id)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var shift = await _db.Shifts.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == id);
            if (shift == null) return NotFound();

            if (shift.Assignment != null)
                _db.ShiftAssignments.Remove(shift.Assignment);

            _db.Shifts.Remove(shift);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/assign")]
        [Authorize(Roles = "CompanyAdmin")]
        public async Task<IActionResult> AssignShift(string slug, int id, [FromBody] AssignShiftViewModel model)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var shift = await _db.Shifts.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == id);
            if (shift == null) return NotFound();

            var targetUser = await _db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == model.UserId && u.TenantId == _tenantContext.CurrentTenantId);
            if (targetUser == null)
                return BadRequest(new { error = "User not found in this tenant" });

            if (shift.Assignment != null)
            {
                shift.Assignment.UserId = model.UserId;
                shift.Assignment.AssignedAt = DateTime.UtcNow;
            }
            else
            {
                _db.ShiftAssignments.Add(new ShiftAssignment
                {
                    TenantId = _tenantContext.CurrentTenantId!.Value,
                    ShiftId = shift.Id,
                    UserId = model.UserId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Shift assigned successfully" });
        }
    }
}
