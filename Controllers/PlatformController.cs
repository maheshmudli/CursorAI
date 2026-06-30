using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Controllers;

[Authorize(Roles = Roles.PlatformOwner)]
public sealed class PlatformController : Controller
{
    private readonly ApplicationDbContext _db;

    public PlatformController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 25;
        var registrations = await _db.RegistrationSubmissions
            .IgnoreQueryFilters()
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var tenants = await _db.Tenants.IgnoreQueryFilters().OrderBy(t => t.CompanyName).ToListAsync();
        var provisioning = await _db.ProvisioningRecords.IgnoreQueryFilters().Include(p => p.Tenant).OrderByDescending(p => p.ProvisionedAt).Take(pageSize).ToListAsync();
        var payments = await _db.PaymentRecords.IgnoreQueryFilters().Include(p => p.Tenant).OrderByDescending(p => p.CreatedAt).Take(pageSize).ToListAsync();
        var seats = await _db.SeatPurchases.IgnoreQueryFilters().Include(s => s.Tenant).OrderByDescending(s => s.CreatedAt).Take(pageSize).ToListAsync();
        var access = await _db.AccessLogs.IgnoreQueryFilters().Include(a => a.User).Include(a => a.Tenant).OrderByDescending(a => a.LastLoginAt).Take(pageSize).ToListAsync();
        var userCounts = await _db.Users.IgnoreQueryFilters().Where(u => u.TenantId != null).GroupBy(u => u.TenantId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count());

        return View(new PlatformDashboardViewModel(registrations, tenants, provisioning, payments, seats, access, userCounts));
    }
}
