using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Platform
{
    [Authorize(Roles = "PlatformOwner")]
    public class PlatformDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlatformDashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var tenants = await _db.Tenants.IgnoreQueryFilters().ToListAsync();
            var registrations = await _db.RegistrationAttempts.ToListAsync();
            var payments = await _db.PaymentRecords.IgnoreQueryFilters().ToListAsync();

            var vm = new PlatformDashboardViewModel
            {
                TotalRegistrations = registrations.Count,
                ActiveTenants = tenants.Count(t => t.Status == TenantStatus.Active),
                TotalUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId != null),
                TotalRevenue = (payments.Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => p.Amount)) / 100m,
                RecentRegistrations = registrations
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(10)
                    .Select(r => new RegistrationSummary
                    {
                        Id = r.Id,
                        CompanyName = r.CompanyName,
                        AdminEmail = r.AdminEmail,
                        Status = r.Status.ToString(),
                        CreatedAt = r.CreatedAt,
                        PaymentIntentId = r.StripePaymentIntentId,
                        TenantId = r.TenantId
                    }).ToList()
            };
            return View(vm);
        }

        public async Task<IActionResult> Tenants(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.Tenants.IgnoreQueryFilters().OrderByDescending(t => t.CreatedAt);
            var total = await query.CountAsync();
            var tenants = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var items = new List<TenantListItem>();
            foreach (var t in tenants)
            {
                var userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id);
                var validationPayment = await _db.PaymentRecords.IgnoreQueryFilters()
                    .Where(p => p.TenantId == t.Id && p.Purpose == "registration")
                    .FirstOrDefaultAsync();

                items.Add(new TenantListItem
                {
                    Id = t.Id,
                    CompanyName = t.CompanyName,
                    Slug = t.Slug,
                    Status = t.Status.ToString(),
                    Tier = t.Tier.ToString(),
                    SeatAllowance = t.SeatAllowance,
                    UserCount = userCount,
                    CreatedAt = t.CreatedAt,
                    ValidationPaymentStatus = validationPayment?.Status.ToString() ?? "None"
                });
            }

            return View(new PlatformTenantsListViewModel
            {
                Tenants = items,
                Page = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalCount = total
            });
        }

        public async Task<IActionResult> TenantDetail(int id)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            var users = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == id)
                .ToListAsync();

            var userRoles = new List<TenantUserSummary>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var accessLog = await _db.AccessLogs.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(al => al.UserId == u.Id);
                userRoles.Add(new TenantUserSummary
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "User",
                    LastLogin = accessLog?.LastLoginAt
                });
            }

            var payments = await _db.PaymentRecords.IgnoreQueryFilters()
                .Where(p => p.TenantId == id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var seatPurchases = await _db.SeatPurchases.IgnoreQueryFilters()
                .Where(sp => sp.TenantId == id)
                .OrderByDescending(sp => sp.CreatedAt)
                .ToListAsync();

            var provisioning = await _db.ProvisioningRecords.IgnoreQueryFilters()
                .FirstOrDefaultAsync(pr => pr.TenantId == id);

            var vm = new TenantDetailViewModel
            {
                Id = tenant.Id,
                CompanyName = tenant.CompanyName,
                Slug = tenant.Slug,
                Route = $"/t/{tenant.Slug}",
                CreatedAt = tenant.CreatedAt,
                Status = tenant.Status.ToString(),
                Tier = tenant.Tier.ToString(),
                SeatAllowance = tenant.SeatAllowance,
                UserCount = users.Count,
                Users = userRoles,
                Payments = payments.Select(p => new PaymentSummary
                {
                    Id = p.Id,
                    StripePaymentIntentId = p.StripePaymentIntentId,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Status = p.Status.ToString(),
                    Purpose = p.Purpose,
                    CreatedAt = p.CreatedAt
                }).ToList(),
                SeatPurchases = seatPurchases.Select(sp => new SeatPurchaseSummary
                {
                    Id = sp.Id,
                    StripePaymentIntentId = sp.StripePaymentIntentId,
                    Amount = sp.Amount,
                    Currency = sp.Currency,
                    Status = sp.Status.ToString(),
                    SeatsAdded = sp.SeatsAdded,
                    CreatedAt = sp.CreatedAt
                }).ToList(),
                Provisioning = provisioning == null ? null : new ProvisioningSummary
                {
                    Id = provisioning.Id,
                    ProvisionedAt = provisioning.ProvisionedAt,
                    Route = provisioning.Route,
                    Status = provisioning.Status.ToString()
                }
            };

            return View(vm);
        }

        public async Task<IActionResult> Registrations(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.RegistrationAttempts.OrderByDescending(r => r.CreatedAt);
            var total = await query.CountAsync();
            var attempts = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return View(new PlatformRegistrationsViewModel
            {
                Attempts = attempts.Select(a => new RegistrationAttemptItem
                {
                    Id = a.Id,
                    CompanyName = a.CompanyName,
                    AdminEmail = a.AdminEmail,
                    Status = a.Status.ToString(),
                    CreatedAt = a.CreatedAt,
                    StripePaymentIntentId = a.StripePaymentIntentId,
                    TenantId = a.TenantId,
                    FailureReason = a.FailureReason
                }).ToList(),
                Page = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalCount = total
            });
        }

        public async Task<IActionResult> AccessLogs(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.AccessLogs.IgnoreQueryFilters()
                .Include(al => al.User)
                .Include(al => al.Tenant)
                .OrderByDescending(al => al.LastLoginAt);

            var total = await query.CountAsync();
            var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var items = new List<AccessLogItem>();
            foreach (var log in logs)
            {
                var roles = log.User != null ? await _userManager.GetRolesAsync(log.User) : new List<string>();
                items.Add(new AccessLogItem
                {
                    Id = log.Id,
                    CompanyName = log.Tenant?.CompanyName ?? "Unknown",
                    UserFullName = log.User?.FullName ?? "Unknown",
                    UserEmail = log.User?.Email ?? "Unknown",
                    Role = roles.FirstOrDefault() ?? "User",
                    LastLoginAt = log.LastLoginAt
                });
            }

            return View(new PlatformAccessLogsViewModel
            {
                Logs = items,
                Page = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalCount = total
            });
        }
    }
}
