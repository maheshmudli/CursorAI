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
[Route("api/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShiftsController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> GetShifts(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? userId)
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var query = _db.Shifts
            .Include(s => s.Assignment)
            .ThenInclude(a => a!.User)
            .AsQueryable();

        if (DateOnly.TryParse(from, out var fromDate))
        {
            query = query.Where(s => s.Date >= fromDate);
        }

        if (DateOnly.TryParse(to, out var toDate))
        {
            query = query.Where(s => s.Date <= toDate);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(s => s.Assignment != null && s.Assignment.UserId == userId);
        }

        var shifts = await query.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();
        return Ok(shifts.Select(MapShift).ToList());
    }

    [HttpPost]
    [Authorize(Roles = Constants.CompanyAdminRole)]
    public async Task<ActionResult<ShiftDto>> CreateShift([FromBody] CreateShiftRequest request)
    {
        if (!await AuthorizeTenantUser()) return Forbid();
        if (!TryParseShiftTimes(request.Date, request.StartTime, request.EndTime, out var date, out var start, out var end, out var error))
        {
            return BadRequest(new ApiError { Message = error! });
        }

        var user = await _userManager.GetUserAsync(User);
        var shift = new Shift
        {
            TenantId = _tenantContext.TenantId!.Value,
            Date = date,
            StartTime = start,
            EndTime = end,
            RoleLabel = request.RoleLabel.Trim(),
            Notes = request.Notes,
            CreatedBy = user?.Id ?? "unknown",
            CreatedAt = DateTime.UtcNow
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return Ok(MapShift(shift));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Constants.CompanyAdminRole)]
    public async Task<ActionResult<ShiftDto>> UpdateShift(int id, [FromBody] UpdateShiftRequest request)
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var shift = await _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();

        if (!TryParseShiftTimes(request.Date, request.StartTime, request.EndTime, out var date, out var start, out var end, out var error))
        {
            return BadRequest(new ApiError { Message = error! });
        }

        shift.Date = date;
        shift.StartTime = start;
        shift.EndTime = end;
        shift.RoleLabel = request.RoleLabel.Trim();
        shift.Notes = request.Notes;
        await _db.SaveChangesAsync();
        return Ok(MapShift(shift));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Constants.CompanyAdminRole)]
    public async Task<IActionResult> DeleteShift(int id)
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var shift = await _db.Shifts.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();

        if (shift.Assignment != null)
        {
            _db.ShiftAssignments.Remove(shift.Assignment);
        }

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Constants.CompanyAdminRole)]
    public async Task<ActionResult<ShiftDto>> AssignShift(int id, [FromBody] AssignShiftRequest request)
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var shift = await _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == _tenantContext.TenantId);
        if (targetUser == null) return BadRequest(new ApiError { Message = "User not found in this tenant." });

        if (shift.Assignment != null)
        {
            shift.Assignment.UserId = request.UserId;
            shift.Assignment.AssignedAt = DateTime.UtcNow;
        }
        else
        {
            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                TenantId = _tenantContext.TenantId!.Value,
                ShiftId = shift.Id,
                UserId = request.UserId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await _db.Entry(shift).Reference(s => s.Assignment).LoadAsync();
        if (shift.Assignment != null)
        {
            await _db.Entry(shift.Assignment).Reference(a => a.User).LoadAsync();
        }

        return Ok(MapShift(shift));
    }

    private async Task<bool> AuthorizeTenantUser()
    {
        if (!_tenantContext.HasTenant) return false;
        if (User.IsInRole(Constants.PlatformOwnerRole)) return false;
        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }

    private static bool TryParseShiftTimes(string dateStr, string startStr, string endStr, out DateOnly date, out TimeOnly start, out TimeOnly end, out string? error)
    {
        error = null;
        date = default;
        start = default;
        end = default;

        if (!DateOnly.TryParse(dateStr, out date))
        {
            error = "Invalid date.";
            return false;
        }

        if (!TimeOnly.TryParse(startStr, out start) || !TimeOnly.TryParse(endStr, out end))
        {
            error = "Invalid start or end time.";
            return false;
        }

        if (end <= start)
        {
            error = "End time must be after start time.";
            return false;
        }

        return true;
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
