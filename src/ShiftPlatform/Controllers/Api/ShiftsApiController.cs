using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Route("t/{slug}/api/shifts")]
public class ShiftsApiController : TenantApiController
{
    private readonly ApplicationDbContext _db;

    public ShiftsApiController(ITenantContext tenant, ApplicationDbContext db) : base(tenant) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDto>>> Get(DateOnly? from, DateOnly? to, string? userId)
    {
        var query = _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.Date >= from.Value);
        if (to.HasValue) query = query.Where(s => s.Date <= to.Value);
        if (!string.IsNullOrEmpty(userId)) query = query.Where(s => s.Assignment != null && s.Assignment.UserId == userId);

        var shifts = await query.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToListAsync();
        return Ok(shifts.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShiftDto>> GetById(int id)
    {
        var shift = await _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(s => s.Id == id);
        return shift == null ? NotFound() : Ok(ToDto(shift));
    }

    [HttpPost]
    public async Task<ActionResult<ShiftDto>> Create(ShiftCreateDto dto)
    {
        if (!IsAdmin) return Forbid();
        if (dto.EndTime <= dto.StartTime) return ValidationError("End time must be after start time.");

        var shift = new Shift
        {
            TenantId = TenantId,
            Date = dto.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RoleLabel = dto.RoleLabel,
            Notes = dto.Notes,
            CreatedBy = CurrentUserId
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { slug = Tenant.Slug, id = shift.Id }, ToDto(shift));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ShiftDto>> Update(int id, ShiftCreateDto dto)
    {
        if (!IsAdmin) return Forbid();
        if (dto.EndTime <= dto.StartTime) return ValidationError("End time must be after start time.");

        var shift = await _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User).FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();

        shift.Date = dto.Date;
        shift.StartTime = dto.StartTime;
        shift.EndTime = dto.EndTime;
        shift.RoleLabel = dto.RoleLabel;
        shift.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return Ok(ToDto(shift));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin) return Forbid();
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();
        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    public async Task<ActionResult<ShiftDto>> Assign(int id, AssignDto dto)
    {
        if (!IsAdmin) return Forbid();

        var shift = await _db.Shifts.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null) return NotFound();

        // Only assign to a user that belongs to this tenant.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId && u.TenantId == TenantId);
        if (user == null) return ValidationError("That user is not part of this workspace.");

        if (shift.Assignment == null)
            _db.ShiftAssignments.Add(new ShiftAssignment { TenantId = TenantId, ShiftId = shift.Id, UserId = user.Id });
        else
        {
            shift.Assignment.UserId = user.Id;
            shift.Assignment.AssignedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        var reloaded = await _db.Shifts.Include(s => s.Assignment).ThenInclude(a => a!.User).FirstAsync(s => s.Id == id);
        return Ok(ToDto(reloaded));
    }

    private ActionResult ValidationError(string message)
        => BadRequest(new { error = message });

    internal static ShiftDto ToDto(Shift s) => new()
    {
        Id = s.Id,
        Date = s.Date,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        RoleLabel = s.RoleLabel,
        Notes = s.Notes,
        AssignmentId = s.Assignment?.Id,
        AssignedUserId = s.Assignment?.UserId,
        AssignedUserName = s.Assignment?.User?.FullName
    };
}
