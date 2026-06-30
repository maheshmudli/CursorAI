using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.CompanyAdmin},{Roles.User}")]
[Route("t/{tenantSlug}/api/shifts")]
public sealed class ShiftsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public ShiftsApiController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Get(DateOnly? from, DateOnly? to, string? userId)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole(Roles.CompanyAdmin);
        var query = _db.Shifts.Include(s => s.Assignments).ThenInclude(a => a.User).AsQueryable();
        if (from.HasValue)
        {
            query = query.Where(s => s.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(s => s.Date <= to.Value);
        }
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(s => s.Assignments.Any(a => a.UserId == userId));
        }
        if (!isAdmin)
        {
            query = query.Where(s => s.Assignments.Any(a => a.UserId == currentUserId));
        }

        var shifts = await query.OrderBy(s => s.Date).ThenBy(s => s.StartTime).Select(s => new ShiftDto(
            s.Id,
            s.Date,
            s.StartTime,
            s.EndTime,
            s.RoleLabel,
            s.Notes,
            s.Assignments.Select(a => a.UserId).FirstOrDefault(),
            s.Assignments.Select(a => a.User.FullName).FirstOrDefault())).ToListAsync();

        return Ok(shifts);
    }

    [HttpPost]
    [Authorize(Roles = Roles.CompanyAdmin)]
    public async Task<IActionResult> Create(ShiftCreateRequest request)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }
        if (request.EndTime <= request.StartTime || string.IsNullOrWhiteSpace(request.RoleLabel))
        {
            return BadRequest(new { error = "A shift requires a role label and an end time after the start time." });
        }

        var shift = new Shift
        {
            TenantId = _tenantProvider.TenantId!.Value,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RoleLabel = request.RoleLabel,
            Notes = request.Notes,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)!
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = shift.Id }, new { shift.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.CompanyAdmin)]
    public async Task<IActionResult> Update(int id, ShiftCreateRequest request)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var shift = await _db.Shifts.SingleOrDefaultAsync(s => s.Id == id);
        if (shift is null)
        {
            return NotFound();
        }
        if (request.EndTime <= request.StartTime || string.IsNullOrWhiteSpace(request.RoleLabel))
        {
            return BadRequest(new { error = "A shift requires a role label and an end time after the start time." });
        }

        shift.Date = request.Date;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.RoleLabel = request.RoleLabel;
        shift.Notes = request.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.CompanyAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var shift = await _db.Shifts.SingleOrDefaultAsync(s => s.Id == id);
        if (shift is null)
        {
            return NotFound();
        }

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Roles.CompanyAdmin)]
    public async Task<IActionResult> Assign(int id, AssignShiftRequest request)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var shift = await _db.Shifts.SingleOrDefaultAsync(s => s.Id == id);
        if (shift is null)
        {
            return NotFound();
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId);
        if (user is null)
        {
            return BadRequest(new { error = "User does not belong to this tenant." });
        }

        var assignment = await _db.ShiftAssignments.SingleOrDefaultAsync(a => a.ShiftId == id);
        if (assignment is null)
        {
            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                TenantId = _tenantProvider.TenantId!.Value,
                ShiftId = id,
                UserId = request.UserId
            });
        }
        else
        {
            assignment.UserId = request.UserId;
            assignment.AssignedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    private bool TenantAllowed()
    {
        var userTenantId = User.FindFirst("tenant_id")?.Value;
        return _tenantProvider.TenantId is not null &&
               userTenantId == _tenantProvider.TenantId.Value.ToString();
    }
}
