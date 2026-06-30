using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Route("t/{slug}/api/swaps")]
public class SwapsApiController : TenantApiController
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SwapsApiController> _logger;

    public SwapsApiController(ITenantContext tenant, ApplicationDbContext db, ILogger<SwapsApiController> logger)
        : base(tenant)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SwapDto>>> Get()
    {
        var swaps = await LoadSwaps().OrderByDescending(s => s.CreatedAt).ToListAsync();
        // Regular users only see swaps that involve them; admins see them all.
        if (!IsAdmin)
            swaps = swaps.Where(s => s.RequestingUserId == CurrentUserId || s.TargetUserId == CurrentUserId).ToList();
        return Ok(swaps.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<SwapDto>> Create(SwapCreateDto dto)
    {
        var requesting = await _db.ShiftAssignments.Include(a => a.Shift).Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == dto.RequestingAssignmentId);
        var target = await _db.ShiftAssignments.Include(a => a.Shift).Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == dto.TargetAssignmentId);

        if (requesting == null || target == null)
            return BadRequest(new { error = "One or both shift assignments could not be found in this workspace." });

        if (requesting.UserId != CurrentUserId)
            return BadRequest(new { error = "You can only request to swap a shift that is assigned to you." });

        if (requesting.Id == target.Id || requesting.UserId == target.UserId)
            return BadRequest(new { error = "Pick another user's shift to swap with." });

        var swap = new SwapRequest
        {
            TenantId = TenantId,
            RequestingUserId = requesting.UserId,
            RequestingAssignmentId = requesting.Id,
            TargetUserId = target.UserId,
            TargetAssignmentId = target.Id,
            Status = SwapStatus.Pending
        };
        _db.SwapRequests.Add(swap);
        await _db.SaveChangesAsync();

        var created = await LoadSwaps().FirstAsync(s => s.Id == swap.Id);
        return Ok(ToDto(created));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<SwapDto>> Approve(int id)
    {
        var swap = await _db.SwapRequests
            .Include(s => s.RequestingAssignment)
            .Include(s => s.TargetAssignment)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (swap == null) return NotFound();
        if (swap.Status != SwapStatus.Pending) return BadRequest(new { error = "This swap has already been resolved." });
        if (!CanApprove(swap)) return Forbid();

        // Exchange the owners of the two assignments.
        var reqUser = swap.RequestingAssignment!.UserId;
        var tgtUser = swap.TargetAssignment!.UserId;
        swap.RequestingAssignment.UserId = tgtUser;
        swap.RequestingAssignment.AssignedAt = DateTime.UtcNow;
        swap.TargetAssignment.UserId = reqUser;
        swap.TargetAssignment.AssignedAt = DateTime.UtcNow;

        swap.Status = SwapStatus.Approved;
        swap.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Swap {SwapId} approved in tenant {TenantId}: assignment {A} <-> {B}.",
            swap.Id, TenantId, swap.RequestingAssignmentId, swap.TargetAssignmentId);

        var updated = await LoadSwaps().FirstAsync(s => s.Id == id);
        return Ok(ToDto(updated));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<SwapDto>> Reject(int id)
    {
        var swap = await _db.SwapRequests.FirstOrDefaultAsync(s => s.Id == id);
        if (swap == null) return NotFound();
        if (swap.Status != SwapStatus.Pending) return BadRequest(new { error = "This swap has already been resolved." });
        if (!CanApprove(swap)) return Forbid();

        swap.Status = SwapStatus.Rejected;
        swap.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var updated = await LoadSwaps().FirstAsync(s => s.Id == id);
        return Ok(ToDto(updated));
    }

    private bool CanApprove(SwapRequest swap)
        => IsAdmin || swap.TargetUserId == CurrentUserId;

    private IQueryable<SwapRequest> LoadSwaps() =>
        _db.SwapRequests
            .Include(s => s.RequestingUser)
            .Include(s => s.TargetUser)
            .Include(s => s.RequestingAssignment)!.ThenInclude(a => a!.Shift)
            .Include(s => s.TargetAssignment)!.ThenInclude(a => a!.Shift);

    private SwapDto ToDto(SwapRequest s) => new()
    {
        Id = s.Id,
        Status = s.Status.ToString(),
        RequestingUserName = s.RequestingUser?.FullName ?? string.Empty,
        TargetUserName = s.TargetUser?.FullName ?? string.Empty,
        RequestingShift = s.RequestingAssignment?.Shift == null ? null : ShiftsApiController.ToDto(s.RequestingAssignment.Shift),
        TargetShift = s.TargetAssignment?.Shift == null ? null : ShiftsApiController.ToDto(s.TargetAssignment.Shift),
        CreatedAt = s.CreatedAt,
        ResolvedAt = s.ResolvedAt,
        CanApprove = s.Status == SwapStatus.Pending && CanApprove(s)
    };
}
