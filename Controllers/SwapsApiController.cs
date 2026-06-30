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
[Route("t/{tenantSlug}/api/swaps")]
public sealed class SwapsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public SwapsApiController(ApplicationDbContext db, ITenantProvider tenantProvider)
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

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var query = _db.SwapRequests
            .Include(s => s.RequestingAssignment).ThenInclude(a => a.Shift)
            .Include(s => s.TargetAssignment).ThenInclude(a => a.Shift)
            .AsQueryable();

        if (!User.IsInRole(Roles.CompanyAdmin))
        {
            query = query.Where(s => s.RequestingUserId == currentUserId || s.TargetUserId == currentUserId);
        }

        var swaps = await query.OrderByDescending(s => s.CreatedAt).Select(s => new
        {
            s.Id,
            s.Status,
            s.RequestingUserId,
            s.TargetUserId,
            RequestingShift = s.RequestingAssignment.Shift.RoleLabel,
            TargetShift = s.TargetAssignment.Shift.RoleLabel,
            s.CreatedAt,
            s.ResolvedAt
        }).ToListAsync();

        return Ok(swaps);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SwapCreateRequest request)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var requesting = await _db.ShiftAssignments.SingleOrDefaultAsync(a => a.Id == request.RequestingAssignmentId);
        var target = await _db.ShiftAssignments.SingleOrDefaultAsync(a => a.Id == request.TargetAssignmentId);
        if (requesting is null || target is null || requesting.UserId != currentUserId || requesting.Id == target.Id)
        {
            return BadRequest(new { error = "Swap assignments must belong to this tenant and the requesting assignment must be yours." });
        }

        _db.SwapRequests.Add(new SwapRequest
        {
            TenantId = _tenantProvider.TenantId!.Value,
            RequestingUserId = currentUserId,
            TargetUserId = target.UserId,
            RequestingAssignmentId = requesting.Id,
            TargetAssignmentId = target.Id
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var swap = await _db.SwapRequests
            .Include(s => s.RequestingAssignment)
            .Include(s => s.TargetAssignment)
            .SingleOrDefaultAsync(s => s.Id == id);
        if (swap is null)
        {
            return NotFound();
        }
        if (swap.Status != SwapStatus.Pending)
        {
            return BadRequest(new { error = "Only pending swaps can be approved." });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!User.IsInRole(Roles.CompanyAdmin) && swap.TargetUserId != currentUserId)
        {
            return Forbid();
        }

        (swap.RequestingAssignment.UserId, swap.TargetAssignment.UserId) =
            (swap.TargetAssignment.UserId, swap.RequestingAssignment.UserId);
        swap.Status = SwapStatus.Approved;
        swap.ResolvedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var swap = await _db.SwapRequests.SingleOrDefaultAsync(s => s.Id == id);
        if (swap is null)
        {
            return NotFound();
        }
        if (swap.Status != SwapStatus.Pending)
        {
            return BadRequest(new { error = "Only pending swaps can be rejected." });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!User.IsInRole(Roles.CompanyAdmin) && swap.TargetUserId != currentUserId)
        {
            return Forbid();
        }

        swap.Status = SwapStatus.Rejected;
        swap.ResolvedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    private bool TenantAllowed() =>
        _tenantProvider.TenantId is not null &&
        User.FindFirst("tenant_id")?.Value == _tenantProvider.TenantId.Value.ToString();
}
