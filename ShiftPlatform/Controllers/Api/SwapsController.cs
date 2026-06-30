using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Contracts;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Services;

namespace ShiftPlatform.Controllers.Api;

[ApiController]
[Authorize(Roles = $"{ApplicationRoles.CompanyAdmin},{ApplicationRoles.User}")]
[Route("t/{tenantSlug}/api/swaps")]
public class SwapsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(string tenantSlug, [FromBody] CreateSwapRequest request)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var requestingAssignment = await dbContext.ShiftAssignments
            .FirstOrDefaultAsync(a => a.Id == request.RequestingAssignmentId && a.UserId == currentUser.Id);
        var targetAssignment = await dbContext.ShiftAssignments
            .FirstOrDefaultAsync(a => a.Id == request.TargetAssignmentId && a.UserId == request.TargetUserId);
        if (requestingAssignment is null || targetAssignment is null)
        {
            return BadRequest(new { error = "Assignment mismatch for swap request." });
        }

        dbContext.SwapRequests.Add(new SwapRequest
        {
            Id = Guid.NewGuid(),
            RequestingUserId = currentUser.Id,
            RequestingAssignmentId = request.RequestingAssignmentId,
            TargetUserId = request.TargetUserId,
            TargetAssignmentId = request.TargetAssignmentId,
            Status = SwapRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Get(string tenantSlug)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var swaps = await dbContext.SwapRequests
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.RequestingUserId,
                s.TargetUserId,
                s.RequestingAssignmentId,
                s.TargetAssignmentId,
                s.Status,
                s.CreatedAt,
                s.ResolvedAt
            })
            .ToListAsync();
        return Ok(swaps);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(string tenantSlug, Guid id)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var swap = await dbContext.SwapRequests
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == SwapRequestStatus.Pending);
        if (swap is null)
        {
            return NotFound();
        }

        var canApprove = User.IsInRole(ApplicationRoles.CompanyAdmin) || swap.TargetUserId == currentUser.Id;
        if (!canApprove)
        {
            return Forbid();
        }

        var requesting = await dbContext.ShiftAssignments.FirstAsync(a => a.Id == swap.RequestingAssignmentId);
        var target = await dbContext.ShiftAssignments.FirstAsync(a => a.Id == swap.TargetAssignmentId);

        (requesting.UserId, target.UserId) = (target.UserId, requesting.UserId);
        requesting.AssignedAt = DateTime.UtcNow;
        target.AssignedAt = DateTime.UtcNow;
        swap.Status = SwapRequestStatus.Approved;
        swap.ResolvedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(string tenantSlug, Guid id)
    {
        if (!IsTenantMatch(tenantSlug))
        {
            return NotFound();
        }

        var swap = await dbContext.SwapRequests
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == SwapRequestStatus.Pending);
        if (swap is null)
        {
            return NotFound();
        }

        swap.Status = SwapRequestStatus.Rejected;
        swap.ResolvedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return Ok();
    }

    private bool IsTenantMatch(string tenantSlug) =>
        tenantContext.IsTenantRoute &&
        string.Equals(tenantContext.CurrentTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase);
}
