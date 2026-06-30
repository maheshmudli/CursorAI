using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Models;

namespace ShiftPlatform.Data;

/// <summary>
/// Seeds roles plus a Platform Owner and one fully provisioned sample tenant
/// (admin, two members, a few shifts and an assignment) for testing.
/// </summary>
public static class DbSeeder
{
    public const string SampleSlug = "acme";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        foreach (var role in PlatformConstants.Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await EnsureUserAsync(userManager, "owner@platform.test", "Platform Owner", "Owner#12345", PlatformConstants.Roles.PlatformOwner, tenantId: null);

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Slug == SampleSlug);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                CompanyName = "Acme Pty Ltd",
                Slug = SampleSlug,
                Status = TenantStatus.Active,
                Tier = TenantTier.Base,
                SeatAllowance = PlatformConstants.IncludedSeats
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            db.ProvisioningRecords.Add(new ProvisioningRecord
            {
                TenantId = tenant.Id,
                Route = $"/t/{tenant.Slug}",
                Status = "Provisioned (seed)"
            });
            db.PaymentRecords.Add(new PaymentRecord
            {
                TenantId = tenant.Id,
                StripePaymentIntentId = "pi_seed_validation",
                Amount = PlatformConstants.ValidationAmountCents,
                Currency = PlatformConstants.Currency,
                Status = "succeeded"
            });
            await db.SaveChangesAsync();
        }

        var admin = await EnsureUserAsync(userManager, "admin@acme.test", "Alice Admin", "Acme#12345", PlatformConstants.Roles.CompanyAdmin, tenant.Id);
        var bob = await EnsureUserAsync(userManager, "bob@acme.test", "Bob Barista", "Acme#12345", PlatformConstants.Roles.User, tenant.Id);
        var carol = await EnsureUserAsync(userManager, "carol@acme.test", "Carol Cook", "Acme#12345", PlatformConstants.Roles.User, tenant.Id);

        var hasShifts = await db.Shifts.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenant.Id);
        if (!hasShifts)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var shift1 = new Shift { TenantId = tenant.Id, Date = today.AddDays(1), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0), RoleLabel = "Front of House", Notes = "Opening shift", CreatedBy = admin.Id };
            var shift2 = new Shift { TenantId = tenant.Id, Date = today.AddDays(2), StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(20, 0), RoleLabel = "Kitchen", CreatedBy = admin.Id };
            var shift3 = new Shift { TenantId = tenant.Id, Date = today.AddDays(3), StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), RoleLabel = "Supervisor", CreatedBy = admin.Id };
            db.Shifts.AddRange(shift1, shift2, shift3);
            await db.SaveChangesAsync();

            db.ShiftAssignments.Add(new ShiftAssignment { TenantId = tenant.Id, ShiftId = shift1.Id, UserId = bob.Id });
            db.ShiftAssignments.Add(new ShiftAssignment { TenantId = tenant.Id, ShiftId = shift2.Id, UserId = carol.Id });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string password, string role, int? tenantId)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                TenantId = tenantId
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}
