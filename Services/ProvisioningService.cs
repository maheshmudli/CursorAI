using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.ViewModels;

namespace ShiftManagementPlatform.Services;

public sealed class ProvisioningService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProvisioningService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<int> CountTenantUsersAsync(int tenantId) =>
        await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId);

    public async Task<ApplicationUser> CreateTenantUserAsync(int tenantId, string fullName, string email, string password, string role)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        var currentCount = await CountTenantUsersAsync(tenantId);
        if (currentCount >= tenant.SeatAllowance)
        {
            throw new InvalidOperationException("The company has reached its paid seat allowance.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            TenantId = tenantId
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, role);
        return user;
    }

    public async Task<Tenant> ProvisionTenantAsync(RegistrationViewModel model, string paymentIntentId, string paymentStatus)
    {
        var slug = await BuildUniqueSlugAsync(model.CompanyName);
        var tenant = new Tenant
        {
            CompanyName = model.CompanyName.Trim(),
            Slug = slug,
            Status = TenantStatus.Active,
            Tier = TenantTier.Base,
            SeatAllowance = 5
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var admin = await CreateTenantUserAsync(tenant.Id, model.AdminFullName, model.AdminEmail, model.AdminPassword, Roles.CompanyAdmin);
        foreach (var user in model.Users.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            await CreateTenantUserAsync(tenant.Id, user.FullName, user.Email, TemporaryPassword(), Roles.User);
        }

        _db.Shifts.AddRange(
            new Shift
            {
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                RoleLabel = "Opening shift",
                Notes = "Seeded from workspace template",
                CreatedBy = admin.Id
            },
            new Shift
            {
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(20, 0),
                RoleLabel = "Closing shift",
                Notes = "Seeded from workspace template",
                CreatedBy = admin.Id
            });

        _db.ProvisioningRecords.Add(new ProvisioningRecord
        {
            TenantId = tenant.Id,
            Route = $"/t/{tenant.Slug}",
            Status = "Provisioned"
        });

        _db.PaymentRecords.Add(new PaymentRecord
        {
            TenantId = tenant.Id,
            StripePaymentIntentId = paymentIntentId,
            Amount = 100,
            Currency = "aud",
            Status = paymentStatus,
            Purpose = PaymentPurpose.RegistrationValidation
        });

        await _db.SaveChangesAsync();
        return tenant;
    }

    private async Task<string> BuildUniqueSlugAsync(string companyName)
    {
        var baseSlug = new string(companyName.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        baseSlug = string.Join('-', baseSlug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "tenant";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    public static string TemporaryPassword() => $"Temp-{Guid.NewGuid():N}aA1!";
}
