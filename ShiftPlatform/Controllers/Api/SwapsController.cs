using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Models.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/swaps")]
public class SwapsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public SwapsController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<SwapRequestDto>>> GetSwaps()
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var user = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole(Constants.CompanyAdminRole);

        var query = _db.SwapRequests
            .Include(s => s.RequestingUser)
            .Include(s => s.TargetUser)
            .Include(s => s.RequestingAssignment).ThenInclude(a => a.Shift)
            .Include(s => s.TargetAssignment).ThenInclude(a => a.Shift)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(s => s.RequestingUserId == user!.Id || s.TargetUserId == user!.Id);
        }

        var swaps = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(swaps.Select(MapSwap).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SwapRequestDto>> CreateSwap([FromBody] CreateSwapRequest request)
    {
        if (!await AuthorizeTenantUser()) return Forbid();

        var user = await _userManager.GetUserAsync(User);
        var requesting = await _db.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.RequestingAssignmentId);

        var target = await _db.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.TargetAssignmentId);

        if (requesting == null || target == null)
        {
            return BadRequest(new ApiError { Message = "One or both assignments were not found." });
        }

        if (requesting.UserId != user!.Id)
        {
            return Forbid();
        }

        if (target.UserId == user.Id)
        {
            return BadRequest(new ApiError { Message = "Cannot swap with your own shift." });
        }

        var existing = await _db.SwapRequests.AnyAsync(s =>
            s.Status == SwapRequestStatus.Pending &&
            (s.RequestingAssignmentId == request.RequestingAssignmentId || s.TargetAssignmentId == request.TargetAssignmentId));

        if (existing)
        {
            return BadRequest(new ApiError { Message = "A pending swap already exists for one of these shifts." });
        }

        var swap = new SwapRequest
        {
            TenantId = _tenantContext.TenantId!.Value,
            RequestingUserId = requesting.UserId,
            RequestingAssignmentId = requesting.Id,
            TargetUserId = target.UserId,
            TargetAssignmentId = target.Id,
            Status = SwapRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.SwapRequests.Add(swap);
        await _db.SaveChangesAsync();

        await _db.Entry(swap).Reference(s => s.RequestingUser).LoadAsync();
        await _db.Entry(swap).Reference(s => s.TargetUser).LoadAsync();
        await _db.Entry(swap).Reference(s => s.RequestingAssignment).LoadAsync();
        await _db.Entry(swap.RequestingAssignment).Reference(a => a.Shift).LoadAsync();
        await _db.Entry(swap).Reference(s => s.TargetAssignment).LoadAsync();
        await _db.Entry(swap.TargetAssignment).Reference(a => a.Shift).LoadAsync();

        return Ok(MapSwap(swap));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<SwapRequestDto>> Approve(int id)
    {
        if (!await AuthorizeTenantUser()) return Forbid();
        return await ResolveSwap(id, approve: true);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<SwapRequestDto>> Reject(int id)
    {
        if (!await AuthorizeTenantUser()) return Forbid();
        return await ResolveSwap(id, approve: false);
    }

    private async Task<ActionResult<SwapRequestDto>> ResolveSwap(int id, bool approve)
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole(Constants.CompanyAdminRole);

        var swap = await _db.SwapRequests
            .Include(s => s.RequestingUser)
            .Include(s => s.TargetUser)
            .Include(s => s.RequestingAssignment).ThenInclude(a => a.Shift)
            .Include(s => s.TargetAssignment).ThenInclude(a => a.Shift)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (swap == null) return NotFound();
        if (swap.Status != SwapRequestStatus.Pending)
        {
            return BadRequest(new ApiError { Message = "Swap request is no longer pending." });
        }

        var canApprove = isAdmin || swap.TargetUserId == user!.Id;
        if (!canApprove)
        {
            return Forbid();
        }

        if (approve)
        {
            var requestingAssignment = await _db.ShiftAssignments.FindAsync(swap.RequestingAssignmentId);
            var targetAssignment = await _db.ShiftAssignments.FindAsync(swap.TargetAssignmentId);
            if (requestingAssignment == null || targetAssignment == null)
            {
                return BadRequest(new ApiError { Message = "Assignments no longer exist." });
            }

            (requestingAssignment.UserId, targetAssignment.UserId) = (targetAssignment.UserId, requestingAssignment.UserId);
            requestingAssignment.AssignedAt = DateTime.UtcNow;
            targetAssignment.AssignedAt = DateTime.UtcNow;
            swap.Status = SwapRequestStatus.Approved;
        }
        else
        {
            swap.Status = SwapRequestStatus.Rejected;
        }

        swap.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(MapSwap(swap));
    }

    private async Task<bool> AuthorizeTenantUser()
    {
        if (!_tenantContext.HasTenant) return false;
        if (User.IsInRole(Constants.PlatformOwnerRole)) return false;
        var user = await _userManager.GetUserAsync(User);
        return user?.TenantId == _tenantContext.TenantId;
    }

    private static SwapRequestDto MapSwap(SwapRequest swap) => new()
    {
        Id = swap.Id,
        RequestingUserName = swap.RequestingUser.FullName,
        TargetUserName = swap.TargetUser.FullName,
        RequestingShiftLabel = $"{swap.RequestingAssignment.Shift.RoleLabel} ({swap.RequestingAssignment.Shift.Date})",
        TargetShiftLabel = $"{swap.TargetAssignment.Shift.RoleLabel} ({swap.TargetAssignment.Shift.Date})",
        Status = swap.Status.ToString(),
        CreatedAt = swap.CreatedAt
    };
}
