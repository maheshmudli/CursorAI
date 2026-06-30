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

namespace ShiftManager.Controllers.Workspace
{
    public class WorkspaceHomeController : WorkspaceBaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;

        // Upcoming window: next 4 weeks
        private const int UpcomingWindowDays = 28;

        public WorkspaceHomeController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
            : base(db, tenantContext)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");
            var now = DateTime.UtcNow.Date;
            var windowEnd = now.AddDays(UpcomingWindowDays);

            // Get shifts with assignments for tenant (filtered by global query filter)
            var shifts = (await _db.Shifts
                .Include(s => s.Assignment)
                    .ThenInclude(a => a != null ? a.User : null)
                .Where(s => s.Date >= now && s.Date <= windowEnd)
                .OrderBy(s => s.Date)
                .ToListAsync())
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
                .ToList();

            Func<Shift, ShiftViewModel> toVm = s => new ShiftViewModel
            {
                Id = s.Id,
                Date = s.Date,
                StartTime = s.StartTime.ToString(@"hh\:mm"),
                EndTime = s.EndTime.ToString(@"hh\:mm"),
                RoleLabel = s.RoleLabel,
                Notes = s.Notes,
                AssignedUserId = s.Assignment?.UserId,
                AssignedUserName = s.Assignment?.User?.FullName,
                CreatedAt = s.CreatedAt,
                CreatedBy = s.CreatedBy
            };

            var myShifts = shifts
                .Where(s => s.Assignment?.UserId == currentUser.Id)
                .Select(toVm).ToList();

            var allShifts = isAdmin ? shifts.Select(toVm).ToList() : new List<ShiftViewModel>();

            // Pending swap requests
            var swaps = await _db.SwapRequests
                .Include(sr => sr.RequestingUser)
                .Include(sr => sr.TargetUser)
                .Include(sr => sr.RequestingAssignment).ThenInclude(a => a != null ? a.Shift : null)
                .Include(sr => sr.TargetAssignment).ThenInclude(a => a != null ? a.Shift : null)
                .Where(sr => sr.Status == SwapStatus.Pending &&
                    (isAdmin || sr.RequestingUserId == currentUser.Id || sr.TargetUserId == currentUser.Id))
                .ToListAsync();

            var vm = new DashboardViewModel
            {
                MyUpcomingShifts = myShifts,
                AllUpcomingShifts = allShifts,
                IsAdmin = isAdmin,
                TenantName = ViewBag.TenantName,
                WindowStart = now,
                WindowEnd = windowEnd,
                PendingSwaps = swaps.Select(sr => new SwapRequestViewModel
                {
                    Id = sr.Id,
                    RequestingUserName = sr.RequestingUser?.FullName ?? "Unknown",
                    TargetUserName = sr.TargetUser?.FullName ?? "Unknown",
                    RequestingShiftDate = sr.RequestingAssignment?.Shift?.Date ?? DateTime.MinValue,
                    RequestingShiftRole = sr.RequestingAssignment?.Shift?.RoleLabel ?? "",
                    TargetShiftDate = sr.TargetAssignment?.Shift?.Date ?? DateTime.MinValue,
                    TargetShiftRole = sr.TargetAssignment?.Shift?.RoleLabel ?? "",
                    Status = sr.Status.ToString(),
                    CreatedAt = sr.CreatedAt
                }).ToList()
            };

            return View(vm);
        }

        public IActionResult Shifts()
        {
            return View();
        }

        public IActionResult Swaps()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RequestSwap(int? assignmentId = null)
        {
            ViewBag.AssignmentId = assignmentId;
            return View();
        }

        public async Task<IActionResult> Users()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "CompanyAdmin");
            if (!isAdmin) return Forbid();

            var tenant = await _db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId!.Value);

            var users = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == _tenantContext.CurrentTenantId)
                .ToListAsync();

            var userVms = new List<TenantUserViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userVms.Add(new TenantUserViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "User",
                    IsActive = u.IsActive,
                    LastLogin = u.LastLoginAt
                });
            }

            ViewBag.SeatAllowance = tenant?.SeatAllowance ?? 5;
            ViewBag.UserCount = users.Count;
            ViewBag.CanAddUser = users.Count < (tenant?.SeatAllowance ?? 5);

            return View(userVms);
        }
    }
}
