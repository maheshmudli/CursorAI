using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers;

/// <summary>
/// Platform Owner tracking dashboard: full visibility over registrations,
/// provisioned workspaces, validation payments, tiers/seats, and access records.
/// All queries span tenants via IgnoreQueryFilters but never expose a tenant's
/// shift data.
/// </summary>
[Authorize(Roles = PlatformConstants.Roles.PlatformOwner)]
public class PlatformController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlatformController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _db.Tenants.IgnoreQueryFilters().AsNoTracking().OrderByDescending(t => t.CreatedAt);
        var total = await query.CountAsync();
        var tenants = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        var rows = new List<TenantOverviewRow>();
        foreach (var t in tenants)
        {
            rows.Add(new TenantOverviewRow
            {
                TenantId = t.Id,
                CompanyName = t.CompanyName,
                Slug = t.Slug,
                Tier = t.Tier,
                Status = t.Status,
                SeatAllowance = t.SeatAllowance,
                CreatedAt = t.CreatedAt,
                UserCount = await _db.Users.CountAsync(u => u.TenantId == t.Id),
                SeatPurchaseCount = await _db.SeatPurchases.CountAsync(s => s.TenantId == t.Id)
            });
        }

        ViewBag.TotalTenants = total;
        ViewBag.TotalUsers = await _db.Users.CountAsync(u => u.TenantId != null);
        ViewBag.TotalProvisioned = await _db.ProvisioningRecords.CountAsync();
        return View(Paged(rows, page, total));
    }

    public async Task<IActionResult> Registrations(int page = 1)
    {
        var query = _db.PendingRegistrations.AsNoTracking().OrderByDescending(p => p.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new RegistrationRow
            {
                CompanyName = p.CompanyName,
                Slug = p.Slug,
                Status = p.Status,
                StripePaymentIntentId = p.StripePaymentIntentId,
                Amount = p.Amount,
                Currency = p.Currency,
                CreatedAt = p.CreatedAt
            }).ToListAsync();
        return View(Paged(items, page, total));
    }

    public async Task<IActionResult> Workspaces(int page = 1)
    {
        var query = from r in _db.ProvisioningRecords.IgnoreQueryFilters().AsNoTracking()
                    join t in _db.Tenants.IgnoreQueryFilters() on r.TenantId equals t.Id
                    orderby r.ProvisionedAt descending
                    select new ProvisioningRecordRow
                    {
                        CompanyName = t.CompanyName,
                        Slug = t.Slug,
                        Route = r.Route,
                        Status = r.Status,
                        ProvisionedAt = r.ProvisionedAt
                    };
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(Paged(items, page, total));
    }

    public async Task<IActionResult> Payments(int page = 1)
    {
        var query = from p in _db.PaymentRecords.IgnoreQueryFilters().AsNoTracking()
                    join t in _db.Tenants.IgnoreQueryFilters() on p.TenantId equals t.Id
                    orderby p.CreatedAt descending
                    select new PaymentRow
                    {
                        CompanyName = t.CompanyName,
                        Slug = t.Slug,
                        StripePaymentIntentId = p.StripePaymentIntentId,
                        Amount = p.Amount,
                        Currency = p.Currency,
                        Status = p.Status,
                        CreatedAt = p.CreatedAt
                    };
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(Paged(items, page, total));
    }

    public async Task<IActionResult> SeatPurchases(int page = 1)
    {
        var query = from s in _db.SeatPurchases.IgnoreQueryFilters().AsNoTracking()
                    join t in _db.Tenants.IgnoreQueryFilters() on s.TenantId equals t.Id
                    orderby s.CreatedAt descending
                    select new SeatPurchaseRow
                    {
                        CompanyName = t.CompanyName,
                        StripePaymentIntentId = s.StripePaymentIntentId,
                        Amount = s.Amount,
                        Currency = s.Currency,
                        Status = s.Status,
                        SeatsAdded = s.SeatsAdded,
                        Applied = s.Applied,
                        CreatedAt = s.CreatedAt
                    };
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(Paged(items, page, total));
    }

    public async Task<IActionResult> AccessRecords(int page = 1)
    {
        var users = await (from u in _db.Users.AsNoTracking()
                           where u.TenantId != null
                           join t in _db.Tenants.IgnoreQueryFilters() on u.TenantId equals t.Id
                           orderby t.CompanyName, u.FullName
                           select new { u.Id, u.FullName, u.Email, t.CompanyName, t.Slug }).ToListAsync();

        var logs = await _db.AccessLogs.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(a => a.UserId, a => a.LastLoginAt);

        var rows = new List<AccessRecordRow>();
        foreach (var u in users)
        {
            var appUser = await _userManager.FindByIdAsync(u.Id);
            var roles = appUser != null ? await _userManager.GetRolesAsync(appUser) : new List<string>();
            rows.Add(new AccessRecordRow
            {
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                CompanyName = u.CompanyName,
                Slug = u.Slug,
                Roles = roles.ToList(),
                LastLoginAt = logs.TryGetValue(u.Id, out var last) ? last : null
            });
        }

        var total = rows.Count;
        var paged = rows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        return View(Paged(paged, page, total));
    }

    private static PagedResult<T> Paged<T>(IReadOnlyList<T> items, int page, int total)
        => new() { Items = items, Page = page, PageSize = PageSize, TotalCount = total };
}
