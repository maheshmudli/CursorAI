using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        foreach (var role in new[] { ApplicationRoles.PlatformOwner, ApplicationRoles.CompanyAdmin, ApplicationRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var ownerEmail = "owner@platform.local";
        var owner = await userManager.FindByEmailAsync(ownerEmail);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                FullName = "Platform Owner",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(owner, "Owner#12345");
            await userManager.AddToRoleAsync(owner, ApplicationRoles.PlatformOwner);
        }

        if (!await db.Tenants.AnyAsync(t => t.Slug == "sampleco"))
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                CompanyName = "SampleCo",
                Slug = "sampleco",
                Status = TenantStatus.Approved,
                Tier = TenantTier.Base,
                SeatAllowance = 5
            };
            db.Tenants.Add(tenant);

            var admin = new ApplicationUser
            {
                UserName = "admin@sampleco.local",
                Email = "admin@sampleco.local",
                FullName = "Sample Admin",
                TenantId = tenant.Id,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin#12345");
            await userManager.AddToRoleAsync(admin, ApplicationRoles.CompanyAdmin);

            var user1 = new ApplicationUser
            {
                UserName = "user1@sampleco.local",
                Email = "user1@sampleco.local",
                FullName = "Sample User One",
                TenantId = tenant.Id,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user1, "User#12345");
            await userManager.AddToRoleAsync(user1, ApplicationRoles.User);

            var user2 = new ApplicationUser
            {
                UserName = "user2@sampleco.local",
                Email = "user2@sampleco.local",
                FullName = "Sample User Two",
                TenantId = tenant.Id,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user2, "User#12345");
            await userManager.AddToRoleAsync(user2, ApplicationRoles.User);

            var shift1 = new Shift
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                RoleLabel = "Morning",
                CreatedBy = admin.Id
            };

            var shift2 = new Shift
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(18, 0),
                RoleLabel = "Day",
                CreatedBy = admin.Id
            };

            db.Shifts.AddRange(shift1, shift2);
            db.ShiftAssignments.AddRange(
                new ShiftAssignment { Id = Guid.NewGuid(), TenantId = tenant.Id, ShiftId = shift1.Id, UserId = user1.Id },
                new ShiftAssignment { Id = Guid.NewGuid(), TenantId = tenant.Id, ShiftId = shift2.Id, UserId = user2.Id });
            db.ProvisioningRecords.Add(new ProvisioningRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Route = "/t/sampleco",
                ProvisionedAt = DateTime.UtcNow,
                Status = "Seeded"
            });
        }

        await db.SaveChangesAsync();
    }
}
