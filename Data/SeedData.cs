using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Data;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.PlatformOwner, Roles.CompanyAdmin, Roles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, "owner@example.com", "Platform Owner", null, Roles.PlatformOwner);

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.Slug == "sample-co");
        if (tenant is null)
        {
            tenant = new Tenant
            {
                CompanyName = "Sample Co",
                Slug = "sample-co",
                Status = TenantStatus.Active,
                Tier = TenantTier.Base,
                SeatAllowance = 5
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            db.ProvisioningRecords.Add(new ProvisioningRecord
            {
                TenantId = tenant.Id,
                Route = "/t/sample-co",
                Status = "Provisioned"
            });

            db.PaymentRecords.Add(new PaymentRecord
            {
                TenantId = tenant.Id,
                StripePaymentIntentId = "pi_sample_validation",
                Amount = 100,
                Currency = "aud",
                Status = "succeeded",
                Purpose = PaymentPurpose.RegistrationValidation
            });
            await db.SaveChangesAsync();
        }

        var admin = await EnsureUserAsync(userManager, "admin@sample.test", "Sample Admin", tenant.Id, Roles.CompanyAdmin);
        var userOne = await EnsureUserAsync(userManager, "alex@sample.test", "Alex User", tenant.Id, Roles.User);
        var userTwo = await EnsureUserAsync(userManager, "casey@sample.test", "Casey User", tenant.Id, Roles.User);

        if (!await db.Shifts.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenant.Id))
        {
            var first = new Shift
            {
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                RoleLabel = "Front desk",
                Notes = "Sample seeded shift",
                CreatedBy = admin.Id
            };
            var second = new Shift
            {
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(18, 0),
                RoleLabel = "Warehouse",
                Notes = "Sample seeded shift",
                CreatedBy = admin.Id
            };
            db.Shifts.AddRange(first, second);
            await db.SaveChangesAsync();

            db.ShiftAssignments.AddRange(
                new ShiftAssignment { TenantId = tenant.Id, ShiftId = first.Id, UserId = userOne.Id },
                new ShiftAssignment { TenantId = tenant.Id, ShiftId = second.Id, UserId = userTwo.Id });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        int? tenantId,
        string role)
    {
        var user = await userManager.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                TenantId = tenantId
            };
            var result = await userManager.CreateAsync(user, "Password1!");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }
}
