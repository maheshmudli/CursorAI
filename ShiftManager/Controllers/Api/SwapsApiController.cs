using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Api
{
    [ApiController]
    [Authorize]
    [Route("t/{slug}/api/swaps")]
    public class SwapsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public SwapsApiController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
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
        public async Task<IActionResult> GetSwaps(string slug)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");

            var query = _db.SwapRequests
                .Include(sr => sr.RequestingUser)
                .Include(sr => sr.TargetUser)
                .Include(sr => sr.RequestingAssignment).ThenInclude(a => a != null ? a.Shift : null)
                .Include(sr => sr.TargetAssignment).ThenInclude(a => a != null ? a.Shift : null)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(sr => sr.RequestingUserId == currentUser.Id || sr.TargetUserId == currentUser.Id);
            }

            var swaps = await query.OrderByDescending(sr => sr.CreatedAt).ToListAsync();

            return Ok(swaps.Select(sr => new
            {
                sr.Id,
                RequestingUser = sr.RequestingUser?.FullName,
                TargetUser = sr.TargetUser?.FullName,
                RequestingShiftDate = sr.RequestingAssignment?.Shift?.Date,
                RequestingShiftRole = sr.RequestingAssignment?.Shift?.RoleLabel,
                TargetShiftDate = sr.TargetAssignment?.Shift?.Date,
                TargetShiftRole = sr.TargetAssignment?.Shift?.RoleLabel,
                Status = sr.Status.ToString(),
                sr.CreatedAt,
                sr.ResolvedAt
            }));
        }

        [HttpPost]
        public async Task<IActionResult> RequestSwap(string slug, [FromBody] CreateSwapRequestViewModel model)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            // Validate requesting assignment belongs to current user
            var myAssignment = await _db.ShiftAssignments
                .Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.Id == model.MyAssignmentId && a.UserId == currentUser.Id);
            if (myAssignment == null)
                return BadRequest(new { error = "Your assignment not found" });

            // Validate target assignment belongs to this tenant
            var targetAssignment = await _db.ShiftAssignments
                .Include(a => a.Shift)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == model.TargetAssignmentId);
            if (targetAssignment == null)
                return BadRequest(new { error = "Target assignment not found" });

            // Check no existing pending swap for these assignments
            var existingSwap = await _db.SwapRequests
                .AnyAsync(sr => sr.Status == SwapStatus.Pending &&
                    (sr.RequestingAssignmentId == model.MyAssignmentId || sr.TargetAssignmentId == model.MyAssignmentId));
            if (existingSwap)
                return BadRequest(new { error = "A pending swap request already exists for this shift" });

            var swap = new SwapRequest
            {
                TenantId = _tenantContext.CurrentTenantId!.Value,
                RequestingUserId = currentUser.Id,
                RequestingAssignmentId = model.MyAssignmentId,
                TargetUserId = targetAssignment.UserId,
                TargetAssignmentId = model.TargetAssignmentId,
                Status = SwapStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SwapRequests.Add(swap);
            await _db.SaveChangesAsync();

            return Ok(new { swap.Id, message = "Swap request created" });
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveSwap(string slug, int id)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");

            var swap = await _db.SwapRequests
                .Include(sr => sr.RequestingAssignment)
                .Include(sr => sr.TargetAssignment)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (swap == null) return NotFound();
            if (swap.Status != SwapStatus.Pending)
                return BadRequest(new { error = "Swap is not pending" });

            // Only admin or target user can approve
            if (!isAdmin && swap.TargetUserId != currentUser.Id)
                return Forbid();

            // Exchange assignments
            var requestingUserId = swap.RequestingAssignment!.UserId;
            var targetUserId = swap.TargetAssignment!.UserId;

            swap.RequestingAssignment.UserId = targetUserId;
            swap.TargetAssignment.UserId = requestingUserId;

            swap.Status = SwapStatus.Approved;
            swap.ResolvedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Swap approved and assignments exchanged" });
        }

        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectSwap(string slug, int id)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");

            var swap = await _db.SwapRequests.FirstOrDefaultAsync(sr => sr.Id == id);
            if (swap == null) return NotFound();
            if (swap.Status != SwapStatus.Pending)
                return BadRequest(new { error = "Swap is not pending" });

            if (!isAdmin && swap.TargetUserId != currentUser.Id)
                return Forbid();

            swap.Status = SwapStatus.Rejected;
            swap.ResolvedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Swap rejected" });
        }
    }
}
