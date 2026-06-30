using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Data;

namespace ShiftPlatform.Controllers;

[Authorize(Roles = ApplicationRoles.PlatformOwner)]
public class PlatformController(ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;

        ViewBag.Registrations = await dbContext.RegistrationSubmissions
            .IgnoreQueryFilters()
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.ProvisioningRecords = await dbContext.ProvisioningRecords
            .IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .OrderByDescending(x => x.ProvisionedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.Payments = await dbContext.PaymentRecords
            .IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.SeatPurchases = await dbContext.SeatPurchases
            .IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.AccessLogs = await dbContext.AccessLogs
            .IgnoreQueryFilters()
            .Include(x => x.User)
            .OrderByDescending(x => x.LastLoginAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.TenantSummaries = await dbContext.Tenants
            .Select(t => new
            {
                Tenant = t,
                UserCount = dbContext.Users.Count(u => u.TenantId == t.Id)
            })
            .OrderByDescending(x => x.Tenant.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return View();
    }
}
