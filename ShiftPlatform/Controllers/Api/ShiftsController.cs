using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Contracts;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

[ApiController]
[Authorize(Roles = $"{ApplicationRoles.CompanyAdmin},{ApplicationRoles.User}")]
[Route("t/{tenantSlug}/api/shifts")]
public class ShiftsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string tenantSlug, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] string? userId)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var query = dbContext.Shifts.AsNoTracking().AsQueryable();
        if (startDate.HasValue)
        {
            query = query.Where(x => x.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.Date <= endDate.Value);
        }

        var shifts = await query
            .Select(x => new
            {
                x.Id,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.RoleLabel,
                x.Notes,
                Assignments = x.Assignments
                    .Where(a => userId == null || a.UserId == userId)
                    .Select(a => new { a.Id, a.UserId })
            })
            .ToListAsync();
        return Ok(shifts);
    }

    [HttpPost]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> Post(string tenantSlug, [FromBody] CreateShiftRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await userManager.GetUserAsync(User);
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RoleLabel = request.RoleLabel,
            Notes = request.Notes,
            CreatedBy = user?.Id ?? "system",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Shifts.Add(shift);
        await dbContext.SaveChangesAsync();
        return Ok(new { shift.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> Put(string tenantSlug, Guid id, [FromBody] UpdateShiftRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var shift = await dbContext.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (shift is null)
        {
            return NotFound();
        }

        shift.Date = request.Date;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.RoleLabel = request.RoleLabel;
        shift.Notes = request.Notes;
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> Delete(string tenantSlug, Guid id)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var shift = await dbContext.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (shift is null)
        {
            return NotFound();
        }

        dbContext.Shifts.Remove(shift);
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = ApplicationRoles.CompanyAdmin)]
    public async Task<IActionResult> Assign(string tenantSlug, Guid id, [FromBody] AssignShiftRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var shift = await dbContext.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (shift is null)
        {
            return NotFound();
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId && x.TenantId == tenantContext.CurrentTenantId);
        if (user is null)
        {
            return BadRequest(new { error = "User does not belong to tenant." });
        }

        var existing = await dbContext.ShiftAssignments.FirstOrDefaultAsync(x => x.ShiftId == id);
        if (existing is null)
        {
            dbContext.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                UserId = user.Id,
                AssignedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.UserId = user.Id;
            existing.AssignedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    private bool IsTenantMatch(string tenantSlug) =>
        tenantContext.IsTenantRoute &&
        string.Equals(tenantContext.CurrentTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase);
}
