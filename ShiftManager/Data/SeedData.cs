using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShiftManager.Models;
using ShiftManager.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var tenantContext = services.GetRequiredService<ITenantContext>();

            // Disable tenant filter for seeding
            tenantContext.CurrentTenantId = null;

            await db.Database.MigrateAsync();

            // Seed roles
            string[] roles = { "PlatformOwner", "CompanyAdmin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed Platform Owner
            const string ownerEmail = "owner@shiftmanager.com";
            if (await userManager.FindByEmailAsync(ownerEmail) == null)
            {
                var owner = new ApplicationUser
                {
                    UserName = ownerEmail,
                    Email = ownerEmail,
                    FullName = "Platform Owner",
                    EmailConfirmed = true,
                    IsActive = true
                };
                await userManager.CreateAsync(owner, "Owner@123456!");
                await userManager.AddToRoleAsync(owner, "PlatformOwner");
            }

            // Seed sample tenant
            if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == "acme-corp"))
            {
                var tenant = new Tenant
                {
                    CompanyName = "Acme Corp",
                    Slug = "acme-corp",
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    Status = TenantStatus.Active,
                    Tier = TenantTier.Base,
                    SeatAllowance = 5
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();

                // Create admin for sample tenant
                const string adminEmail = "admin@acme-corp.com";
                ApplicationUser? adminUser = null;
                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = "Alice Admin",
                        TenantId = tenant.Id,
                        EmailConfirmed = true,
                        IsActive = true
                    };
                    await userManager.CreateAsync(adminUser, "Admin@123456!");
                    await userManager.AddToRoleAsync(adminUser, "CompanyAdmin");
                }
                else
                {
                    adminUser = await userManager.FindByEmailAsync(adminEmail);
                }

                // Create sample users
                var sampleUsers = new[]
                {
                    ("Bob Worker", "bob@acme-corp.com", "Bob@123456!"),
                    ("Carol Staff", "carol@acme-corp.com", "Carol@123456!")
                };

                foreach (var (name, email, password) in sampleUsers)
                {
                    if (await userManager.FindByEmailAsync(email) == null)
                    {
                        var user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            FullName = name,
                            TenantId = tenant.Id,
                            EmailConfirmed = true,
                            IsActive = true
                        };
                        await userManager.CreateAsync(user, password);
                        await userManager.AddToRoleAsync(user, "User");
                    }
                }

                // Create provisioning record
                if (!await db.ProvisioningRecords.IgnoreQueryFilters().AnyAsync(pr => pr.TenantId == tenant.Id))
                {
                    db.ProvisioningRecords.Add(new ProvisioningRecord
                    {
                        TenantId = tenant.Id,
                        ProvisionedAt = DateTime.UtcNow.AddDays(-30),
                        Route = "/t/acme-corp",
                        Status = ProvisioningStatus.Completed
                    });
                }

                // Create payment record
                if (!await db.PaymentRecords.IgnoreQueryFilters().AnyAsync(pr => pr.TenantId == tenant.Id))
                {
                    db.PaymentRecords.Add(new PaymentRecord
                    {
                        TenantId = tenant.Id,
                        StripePaymentIntentId = "pi_sample_registration",
                        Amount = 100,
                        Currency = "aud",
                        Status = PaymentStatus.Succeeded,
                        CreatedAt = DateTime.UtcNow.AddDays(-30),
                        Purpose = "registration"
                    });
                }

                await db.SaveChangesAsync();

                // Create sample shifts
                tenantContext.CurrentTenantId = tenant.Id;
                if (!await db.Shifts.AnyAsync())
                {
                    var adminUserId = adminUser?.Id ?? "";
                    var bob = await userManager.FindByEmailAsync("bob@acme-corp.com");
                    var carol = await userManager.FindByEmailAsync("carol@acme-corp.com");

                    var today = DateTime.UtcNow.Date;
                    var shifts = new[]
                    {
                        new Shift { TenantId = tenant.Id, Date = today.AddDays(1), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Morning Shift", Notes = "Opening shift", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                        new Shift { TenantId = tenant.Id, Date = today.AddDays(1), StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoleLabel = "Afternoon Shift", Notes = "Closing shift", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                        new Shift { TenantId = tenant.Id, Date = today.AddDays(2), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Morning Shift", Notes = null, CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                        new Shift { TenantId = tenant.Id, Date = today.AddDays(3), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Morning Shift", Notes = "Weekend shift", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                        new Shift { TenantId = tenant.Id, Date = today.AddDays(7), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Manager Shift", Notes = "Next week", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow }
                    };
                    db.Shifts.AddRange(shifts);
                    await db.SaveChangesAsync();

                    // Assign shifts to users
                    if (bob != null)
                    {
                        db.ShiftAssignments.Add(new ShiftAssignment
                        {
                            TenantId = tenant.Id,
                            ShiftId = shifts[0].Id,
                            UserId = bob.Id,
                            AssignedAt = DateTime.UtcNow
                        });
                    }
                    if (carol != null)
                    {
                        db.ShiftAssignments.Add(new ShiftAssignment
                        {
                            TenantId = tenant.Id,
                            ShiftId = shifts[1].Id,
                            UserId = carol.Id,
                            AssignedAt = DateTime.UtcNow
                        });
                    }
                    await db.SaveChangesAsync();
                }

                tenantContext.CurrentTenantId = null;

                // Registration attempt record
                if (!await db.RegistrationAttempts.AnyAsync(r => r.CompanyName == "Acme Corp"))
                {
                    db.RegistrationAttempts.Add(new RegistrationAttempt
                    {
                        CompanyName = "Acme Corp",
                        AdminEmail = "admin@acme-corp.com",
                        Status = RegistrationStatus.Completed,
                        CreatedAt = DateTime.UtcNow.AddDays(-30),
                        CompletedAt = DateTime.UtcNow.AddDays(-30),
                        StripePaymentIntentId = "pi_sample_registration",
                        TenantId = tenant.Id
                    });
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
