using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        await EnsureRolesAsync(roleManager);
        await EnsurePlatformOwnerAsync(userManager);
        await EnsureSampleTenantAsync(db, userManager, scope.ServiceProvider.GetRequiredService<ITenantContext>());
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Constants.PlatformOwnerRole, Constants.CompanyAdminRole, Constants.UserRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task EnsurePlatformOwnerAsync(UserManager<ApplicationUser> userManager)
    {
        const string email = "owner@platform.local";
        if (await userManager.FindByEmailAsync(email) != null) return;

        var owner = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Platform Owner",
            EmailConfirmed = true
        };

        await userManager.CreateAsync(owner, "OwnerPass123!");
        await userManager.AddToRoleAsync(owner, Constants.PlatformOwnerRole);
    }

    private static async Task EnsureSampleTenantAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantContext tenantContext)
    {
        const string slug = "acme-corp";
        if (await db.Tenants.AnyAsync(t => t.Slug == slug)) return;

        var tenant = new Tenant
        {
            CompanyName = "Acme Corp",
            Slug = slug,
            CreatedAt = DateTime.UtcNow,
            Status = TenantStatus.Active,
            Tier = TenantTier.Base,
            SeatAllowance = Constants.IncludedSeats
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        tenantContext.SetTenant(tenant.Id, tenant.Slug);

        var admin = new ApplicationUser
        {
            UserName = "admin@acme.local",
            Email = "admin@acme.local",
            FullName = "Acme Admin",
            TenantId = tenant.Id,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "AdminPass123!");
        await userManager.AddToRoleAsync(admin, Constants.CompanyAdminRole);

        var user1 = new ApplicationUser
        {
            UserName = "alice@acme.local",
            Email = "alice@acme.local",
            FullName = "Alice Smith",
            TenantId = tenant.Id,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user1, "UserPass123!");
        await userManager.AddToRoleAsync(user1, Constants.UserRole);

        var user2 = new ApplicationUser
        {
            UserName = "bob@acme.local",
            Email = "bob@acme.local",
            FullName = "Bob Jones",
            TenantId = tenant.Id,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user2, "UserPass123!");
        await userManager.AddToRoleAsync(user2, Constants.UserRole);

        db.ProvisioningRecords.Add(new ProvisioningRecord
        {
            TenantId = tenant.Id,
            ProvisionedAt = DateTime.UtcNow,
            Route = $"/{Constants.TenantRoutePrefix}/{tenant.Slug}",
            Status = ProvisioningStatus.Completed
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shifts = new[]
        {
            new Shift { TenantId = tenant.Id, Date = today.AddDays(1), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0), RoleLabel = "Front Desk", CreatedBy = admin.Id, CreatedAt = DateTime.UtcNow },
            new Shift { TenantId = tenant.Id, Date = today.AddDays(2), StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(21, 0), RoleLabel = "Support", CreatedBy = admin.Id, CreatedAt = DateTime.UtcNow },
            new Shift { TenantId = tenant.Id, Date = today.AddDays(3), StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), RoleLabel = "Operations", CreatedBy = admin.Id, CreatedAt = DateTime.UtcNow }
        };

        db.Shifts.AddRange(shifts);
        await db.SaveChangesAsync();

        db.ShiftAssignments.Add(new ShiftAssignment
        {
            TenantId = tenant.Id,
            ShiftId = shifts[0].Id,
            UserId = user1.Id,
            AssignedAt = DateTime.UtcNow
        });

        db.ShiftAssignments.Add(new ShiftAssignment
        {
            TenantId = tenant.Id,
            ShiftId = shifts[1].Id,
            UserId = user2.Id,
            AssignedAt = DateTime.UtcNow
        });

        db.AccessLogs.AddRange(
            new AccessLog { TenantId = tenant.Id, UserId = admin.Id, LastLoginAt = DateTime.UtcNow.AddDays(-1) },
            new AccessLog { TenantId = tenant.Id, UserId = user1.Id, LastLoginAt = DateTime.UtcNow.AddHours(-3) }
        );

        db.RegistrationSubmissions.Add(new RegistrationSubmission
        {
            CompanyName = tenant.CompanyName,
            AdminFullName = admin.FullName,
            AdminEmail = admin.Email!,
            Status = RegistrationStatus.Completed,
            SubmittedAt = DateTime.UtcNow.AddDays(-7),
            TenantId = tenant.Id,
            StripePaymentIntentId = "pi_seed_sample"
        });

        db.PaymentRecords.Add(new PaymentRecord
        {
            TenantId = tenant.Id,
            StripePaymentIntentId = "pi_seed_sample",
            Amount = Constants.ValidationAmountCents,
            Currency = Constants.Currency,
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        });

        await db.SaveChangesAsync();
        tenantContext.Clear();
    }
}
