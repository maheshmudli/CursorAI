using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.ViewModels;

namespace ShiftPlatform.Controllers.Platform;

[Authorize(Roles = Constants.PlatformOwnerRole)]
public class PlatformDashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private const int PageSize = 10;

    public PlatformDashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(1, page);

        var vm = new PlatformDashboardViewModel { Page = page };

        var registrationQuery = _db.RegistrationSubmissions.OrderByDescending(r => r.SubmittedAt);
        var totalRegistrations = await registrationQuery.CountAsync();
        vm.TotalPages = Math.Max(1, (int)Math.Ceiling(totalRegistrations / (double)PageSize));

        vm.Registrations = await registrationQuery
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(r => new RegistrationRow
            {
                Id = r.Id,
                CompanyName = r.CompanyName,
                AdminEmail = r.AdminEmail,
                Status = r.Status.ToString(),
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync();

        vm.Workspaces = await _db.ProvisioningRecords
            .Include(p => p.Tenant)
            .OrderByDescending(p => p.ProvisionedAt)
            .Take(50)
            .Select(p => new WorkspaceRow
            {
                Id = p.Id,
                CompanyName = p.Tenant.CompanyName,
                Route = p.Route,
                ProvisionedAt = p.ProvisionedAt,
                Status = p.Status.ToString()
            })
            .ToListAsync();

        vm.ValidationPayments = await _db.PaymentRecords
            .Include(p => p.Tenant)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .Select(p => new PaymentRow
            {
                Id = p.Id,
                CompanyName = p.Tenant.CompanyName,
                Amount = p.Amount,
                Currency = p.Currency,
                StripePaymentIntentId = p.StripePaymentIntentId,
                Status = p.Status.ToString()
            })
            .ToListAsync();

        var tenants = await _db.Tenants
            .OrderBy(t => t.CompanyName)
            .Take(50)
            .ToListAsync();

        foreach (var tenant in tenants)
        {
            var userCount = await _db.Users.CountAsync(u => u.TenantId == tenant.Id);
            var purchases = await _db.SeatPurchases
                .Where(s => s.TenantId == tenant.Id)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SeatPurchaseRow
                {
                    Amount = s.Amount,
                    Currency = s.Currency,
                    StripePaymentIntentId = s.StripePaymentIntentId,
                    Status = s.Status.ToString(),
                    SeatsAdded = s.SeatsAdded,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            vm.TenantSummaries.Add(new TenantSummaryRow
            {
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                Tier = tenant.Tier.ToString(),
                SeatAllowance = tenant.SeatAllowance,
                UserCount = userCount,
                SeatPurchases = purchases
            });
        }

        vm.AccessRecords = await _db.AccessLogs
            .IgnoreQueryFilters()
            .Include(a => a.Tenant)
            .Include(a => a.User)
            .OrderByDescending(a => a.LastLoginAt)
            .Take(50)
            .Select(a => new AccessRow
            {
                CompanyName = a.Tenant.CompanyName,
                UserName = a.User.FullName,
                Email = a.User.Email!,
                LastLoginAt = a.LastLoginAt
            })
            .ToListAsync();

        return View(vm);
    }
}
